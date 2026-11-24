"""只读核对公共领域原著的关键摘录，避免 Windows 管道损坏中文网址。"""

from concurrent.futures import ThreadPoolExecutor
import html
import re
import urllib.parse
import urllib.request


CHECKS = {
    1: ["面如重棗", "丹鳳眼", "豹頭環眼"],
    5: ["三叉束髮紫金冠", "離洛陽五十里", "關前紮住大寨"],
    7: ["濃眉大眼", "闊面重頤"],
    15: ["姿質風流", "儀容秀麗"],
    38: ["面如冠玉", "頭戴綸巾", "身披鶴氅"],
    53: ["年近六旬"],
}


def check_chapter(entry):
    chapter, phrases = entry
    path = f"三國演義/第{chapter:03d}回"
    assert "?" not in path, "中文路径不应被管道转换为问号"
    url = "https://zh.wikisource.org/wiki/" + urllib.parse.quote(path)
    request = urllib.request.Request(url, headers={"User-Agent": "LiteratureReferenceCheck/1.0"})
    with urllib.request.urlopen(request, timeout=25) as response:
        raw = response.read().decode("utf-8")
        status = response.status
    plain = html.unescape(re.sub("<[^>]*>", "", raw))
    compact = re.sub(r"\s+", "", plain)
    rows = []
    for phrase in phrases:
        index = compact.find(phrase)
        context = compact[max(0, index-20):index+len(phrase)+30] if index >= 0 else ""
        rows.append((phrase, index >= 0, context))
    return chapter, status, url, rows


if __name__ == "__main__":
    all_matched = True
    with ThreadPoolExecutor(max_workers=3) as pool:
        for chapter, status, url, rows in pool.map(check_chapter, CHECKS.items()):
            print(f"第{chapter:03d}回 HTTP {status}：{url}")
            for phrase, matched, context in rows:
                print(f"  {'已核对' if matched else '未匹配'}：{phrase}；{context}")
                all_matched = all_matched and matched
    if not all_matched:
        raise SystemExit("有摘录未匹配，需人工复核版本或页面内容。")
    print(f"{len(CHECKS)}回原文、{sum(len(phrases) for phrases in CHECKS.values())}项短语均已核对。")
