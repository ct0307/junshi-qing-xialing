#!/usr/bin/env python3
"""仅下载并校验 Unity 官方安装包，不执行或提取任何安装器。"""
import argparse
import base64
import concurrent.futures as futures
import email.parser
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
import time
import urllib.parse
import urllib.request
from pathlib import Path, PureWindowsPath

HOSTS = {"download.unity3d.com", "public-cdn.cloud.unity3d.com", "dl.google.com"}
REDIRECT_HOSTS = HOSTS | {"download.unitychina.cn", "public-cdn.cloud.unitychina.cn"}


class RangeValidationError(ValueError):
    """仅承载本工具生成的安全校验错误，不包含代理详情。"""


def check_url(url, redirect=False):
    parsed = urllib.parse.urlsplit(url)
    if (parsed.scheme != "https" or parsed.hostname not in (REDIRECT_HOSTS if redirect else HOSTS)
            or parsed.username or parsed.password or parsed.port not in (None, 443)):
        raise ValueError("下载地址必须使用允许的官方 HTTPS 域名，且不得包含凭据")


class OfficialRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        check_url(newurl, redirect=True)
        return super().redirect_request(req, fp, code, msg, headers, newurl)


def safe_path(root, *names):
    target = root.joinpath(*names)
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError("目标路径越过输出目录，请检查符号链接或目录联接")
    return target


def validate_package(package):
    package_id, name = package["Id"], package["FileName"]
    if not isinstance(package_id, str) or not re.fullmatch(r"[A-Za-z0-9_-]+", package_id):
        raise ValueError("Id 只能使用英文字母、数字、下划线和连字符")
    if (not isinstance(name, str) or not name or name in (".", "..")
            or PureWindowsPath(name).name != name or name[-1] in ". "
            or any(ord(char) < 32 or char in '<>:"/\\|?*' for char in name)
            or re.fullmatch(r"(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])", name.split(".")[0])):
        raise ValueError("FileName 必须是安全的单个文件名")
    if type(package["Size"]) is not int or package["Size"] <= 0:
        raise ValueError("Size 必须是正整数")
    check_url(package["Url"])
    integrity = package.get("Integrity") or ""
    if integrity:
        algorithm, digest = integrity.split("-", 1)
        expected = base64.b64decode(digest, validate=True)
        if algorithm not in ("md5", "sha384", "sha512") or len(expected) != hashlib.new(algorithm).digest_size:
            raise ValueError("Integrity 只支持 md5-base64、sha384-base64 或 sha512-base64")


def verify_file(path, package):
    if not path.is_file() or path.stat().st_size != package["Size"]:
        return False
    integrity = package.get("Integrity") or ""
    if not integrity:
        return True
    algorithm, expected = integrity.split("-", 1)
    digest = hashlib.new(algorithm)
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return base64.b64encode(digest.digest()).decode("ascii") == expected


def check_response(status, headers, package, start, end):
    if status != 206 or headers.get("Content-Range") != f"bytes {start}-{end}/{package['Size']}":
        raise RangeValidationError("服务器未返回预期的 Content-Range 或 206 状态")
    if headers.get("Content-Encoding", "identity").lower() != "identity":
        raise RangeValidationError("服务器返回了不支持的压缩编码")
    declared = headers.get("Content-Length")
    if declared is not None and (not declared.isascii() or not declared.isdigit() or int(declared) != end - start + 1):
        raise RangeValidationError("服务器声明的分段长度不符")


def download_curl(package, path, start, end, proxies, deadline):
    url = package["Url"]
    for hop in range(11):
        check_url(url, redirect=hop > 0)
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise RangeValidationError("curl 尝试总时限超过 90 秒")
        with tempfile.NamedTemporaryFile(dir=path.parent, prefix=path.name + ".", suffix=".pending", delete=False) as temporary:
            pending = Path(temporary.name)
        command = ["curl.exe" if os.name == "nt" else "curl", "--disable", "--silent", "--show-error",
                   "--proto", "=https", "--connect-timeout", str(min(15, remaining)),
                   "--max-time", str(remaining), "--max-filesize", str(end - start + 1),
                   "--dump-header", "-", "--output", str(pending),
                   "--header", f"Range: bytes={start}-{end}", "--header", "Accept-Encoding: identity",
                   "--proxy", proxies.get("https") or proxies.get("all") or "",
                   "--noproxy", proxies.get("no", ""), "--url", url]
        try:
            result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                    timeout=remaining, shell=False)
        except subprocess.TimeoutExpired:
            raise RangeValidationError("curl 尝试总时限超过 90 秒") from None
        if result.returncode:
            raise RangeValidationError(f"curl 退出码 {result.returncode}")
        # 代理 CONNECT 和 1xx 可能产生多个响应头，仅解析最后一个 HTTP 响应。
        blocks = [block for block in re.split(rb"\r?\n\r?\n", result.stdout)
                  if re.match(rb"^HTTP/\S+\s+\d{3}(?:\s|$)", block)]
        if not blocks:
            raise RangeValidationError("curl 未返回有效的 HTTP 响应头")
        status_line, _, raw_headers = blocks[-1].partition(b"\n")
        status = int(status_line.split()[1])
        headers = email.parser.BytesParser().parsebytes(raw_headers)
        if status in (301, 302, 303, 307, 308):
            if hop == 10 or not headers.get("Location"):
                raise RangeValidationError("curl 重定向次数过多或缺少目标地址")
            url = urllib.parse.urljoin(url, headers["Location"])
            check_url(url, redirect=True)
            continue
        check_response(status, headers, package, start, end)
        if headers.get("Content-Length") is None:
            raise RangeValidationError("curl 响应缺少 Content-Length")
        if pending.stat().st_size != end - start + 1:
            raise RangeValidationError(f"分段实际长度不符（预期 {end - start + 1}，实际 {pending.stat().st_size}）")
        if time.monotonic() > deadline:
            raise RangeValidationError("curl 尝试总时限超过 90 秒")
        if path.exists():
            raise FileExistsError("正式分段已存在，拒绝覆盖")
        pending.rename(path)
        return end - start + 1


def download_part(package, path, start, end, proxies, backend="urllib"):
    length = end - start + 1
    if path.exists():
        if path.is_file() and path.stat().st_size == length:
            return length
        raise ValueError("已有分段长度不符，已保留，请人工检查：" + path.name)
    # 完整校验后才创建正式分段；curl 失败体保留为独占的 .pending 文件。
    for attempt in range(3):
        deadline = time.monotonic() + (90 if backend == "curl" else 60)
        try:
            if backend == "curl":
                return download_curl(package, path, start, end, proxies, deadline)
            opener = urllib.request.build_opener(urllib.request.ProxyHandler(proxies), OfficialRedirect())
            request = urllib.request.Request(package["Url"], headers={
                "Range": f"bytes={start}-{end}", "Accept-Encoding": "identity"})
            with opener.open(request, timeout=60) as response:
                check_response(response.status, response.headers, package, start, end)
                data = bytearray()
                while len(data) <= length:
                    if response.fp is None:
                        break
                    remaining = deadline - time.monotonic()
                    if remaining <= 0:
                        raise TimeoutError("分段下载超过 60 秒")
                    response.fp.raw._sock.settimeout(remaining)
                    block = response.read1(min(1024 * 1024, length + 1 - len(data)))
                    if not block:
                        break
                    data.extend(block)
                if len(data) != length:
                    raise RangeValidationError(f"分段实际长度不符（预期 {length}，实际 {len(data)}）")
                if time.monotonic() > deadline:
                    raise RangeValidationError("分段下载超过 60 秒")
            # 独占创建，其他进程产生的既有分段也不会被覆盖。
            with path.open("xb") as target:
                target.write(data)
            return length
        except (OSError, ValueError, AttributeError) as error:
            if attempt == 2:
                reason = str(error) if isinstance(error, RangeValidationError) else type(error).__name__
                raise RuntimeError(f"分段 {path.name} 下载失败（{reason}，已尝试 3 次）") from None


def merge_parts(parts, partial):
    initial_size = partial.stat().st_size if partial.exists() else 0
    if initial_size > sum(path.stat().st_size for path in parts):
        raise ValueError("已有 .partial 过长，已保留")
    # 验证已有前缀再继续追加；不截断已有文件，不删除任何分段。
    with partial.open("a+b") as target:
        target.seek(0)
        position = 0
        for path in parts:
            with path.open("rb") as source:
                for block in iter(lambda: source.read(1024 * 1024), b""):
                    overlap = min(max(initial_size - position, 0), len(block))
                    if overlap and target.read(overlap) != block[:overlap]:
                        raise ValueError("已有 .partial 内容与分段不符，已保留")
                    if overlap < len(block):
                        target.seek(0, 2)
                        target.write(block[overlap:])
                    position += len(block)


def download_package(package, root, workers, chunk_size, proxies, backend="urllib"):
    package_id = package["Id"]
    final = safe_path(root, package["FileName"])
    if final.exists():
        if not verify_file(final, package):
            raise ValueError(f"{package_id} 已有最终文件不匹配，拒绝覆盖")
        print(f"{package_id}：已有文件校验通过，跳过", flush=True)
        return
    part_dir = safe_path(root, "Parts", package_id)
    part_dir.mkdir(parents=True, exist_ok=True)
    parts = [safe_path(root, "Parts", package_id, f"part-{index:05d}.bin")
             for index in range((package["Size"] + chunk_size - 1) // chunk_size)]
    completed = 0
    print(f"{package_id}：开始下载，共 {package['Size']} 字节，{len(parts)} 段", flush=True)
    with futures.ThreadPoolExecutor(max_workers=workers) as executor:
        pending = {executor.submit(download_part, package, path, index * chunk_size,
                   min((index + 1) * chunk_size, package["Size"]) - 1, proxies, backend)
                   for index, path in enumerate(parts)}
        next_report = time.monotonic() + 30
        while pending:
            done, pending = futures.wait(pending, timeout=max(0, next_report - time.monotonic()),
                                         return_when=futures.FIRST_COMPLETED)
            try:
                completed += sum(task.result() for task in done)
            except Exception:
                for task in pending:
                    task.cancel()
                raise
            if time.monotonic() >= next_report or not pending:
                print(f"{package_id}：累计完成 {completed}/{package['Size']} 字节 "
                      f"({completed / package['Size']:.1%})", flush=True)
                next_report = time.monotonic() + 30
    partial = safe_path(root, package["FileName"] + ".partial")
    merge_parts(parts, partial)
    if not verify_file(partial, package):
        raise ValueError(f"{package_id} 合并后尺寸或官方哈希不符，保留 .partial，未生成最终文件")
    if final.exists():
        raise ValueError(f"{package_id} 最终文件在下载过程中出现，拒绝覆盖")
    partial.rename(final)
    mode = "尺寸及官方哈希" if package.get("Integrity") else "尺寸（清单未提供哈希）"
    print(f"{package_id}：{mode}校验通过，已保存 {final.name}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True, help="官方包下载清单 JSON 路径")
    parser.add_argument("--output-dir", type=Path, default=Path("F:/Development/Unity/Downloads"), help="下载输出目录")
    parser.add_argument("--workers", type=int, default=4, help="并发分段数，默认 4")
    parser.add_argument("--chunk-mib", type=int, default=8, help="每段 MiB 数，默认 8")
    parser.add_argument("--backend", choices=("curl", "urllib"), default="curl" if os.name == "nt" else "urllib",
                        help="传输后端，Windows 默认使用系统 curl.exe")
    parser.add_argument("--only", nargs="+", help="仅下载指定 Id，以空格分隔")
    args = parser.parse_args()
    if args.workers <= 0 or args.chunk_mib <= 0:
        raise ValueError("workers 和 chunk-mib 必须大于 0")
    packages = json.loads(args.manifest.read_text(encoding="utf-8-sig"))["Packages"]
    if not isinstance(packages, list) or not packages:
        raise ValueError("Packages 必须是非空数组")
    ids, names = set(), set()
    for package in packages:
        validate_package(package)
        if package["Id"].casefold() in ids or package["FileName"].casefold() in names:
            raise ValueError("清单包含重复 Id 或 FileName")
        ids.add(package["Id"].casefold())
        names.add(package["FileName"].casefold())
    if args.only and any(value.casefold() not in ids for value in args.only):
        raise ValueError("only 包含清单中不存在的 Id")
    args.output_dir.mkdir(parents=True, exist_ok=True)
    proxies = urllib.request.getproxies()
    for package in packages:
        if not args.only or package["Id"].casefold() in {value.casefold() for value in args.only}:
            download_package(package, args.output_dir, args.workers, args.chunk_mib * 1024 * 1024, proxies, args.backend)


if __name__ == "__main__":
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")
    try:
        main()
    except Exception as error:
        # 不输出底层网络异常文本，避免系统代理地址或凭据泄漏。
        message = str(error) if isinstance(error, (ValueError, RuntimeError)) else type(error).__name__
        print("下载失败：" + message, file=sys.stderr, flush=True)
        sys.exit(1)
