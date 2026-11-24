// 将本轮计划和验收Markdown同步为可编辑Word；不安装依赖。
const fs = require('fs');
const path = require('path');
const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
  Header, Footer, AlignmentType, HeadingLevel, LevelFormat, WidthType,
  BorderStyle, ShadingType, PageNumber,
} = require('docx');

const root = path.resolve(__dirname, '..');
const plain = value => value.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '$1（$2）').replace(/`/g, '').replace(/\*\*/g, '');
const paragraph = (text, extra = {}) => new Paragraph({
  spacing: { after: 100, line: 320 }, ...extra,
  children: [new TextRun({ text: plain(text), font: 'Microsoft YaHei' })],
});
function table(lines) {
  const rows = lines.filter(line => !/^\|[\s:|\-]+\|$/.test(line))
    .map(line => line.replace(/^\|/, '').replace(/\|$/, '').split('|').map(x => x.trim()));
  const count = rows[0].length;
  const widths = Array.from({ length: count }, (_, i) => Math.floor(9746 / count) + (i === count - 1 ? 9746 % count : 0));
  const border = { style: BorderStyle.SINGLE, size: 4, color: 'C5D7CF' };
  return new Table({
    width: { size: 9746, type: WidthType.DXA }, columnWidths: widths,
    rows: rows.map((cells, index) => new TableRow({ tableHeader: index === 0, cantSplit: true,
      children: cells.map((cell, i) => new TableCell({
        width: { size: widths[i], type: WidthType.DXA },
        borders: { top: border, bottom: border, left: border, right: border },
        shading: { type: ShadingType.CLEAR, fill: index === 0 ? 'E2EDE7' : 'FFFFFF' },
        margins: { top: 90, bottom: 90, left: 110, right: 110 },
        children: [paragraph(cell, { spacing: { after: 40, line: 290 } })],
      })),
    })),
  });
}
function children(markdown) {
  const result = []; const lines = markdown.replace(/\r/g, '').split('\n'); let code = false;
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (line.startsWith('```')) { code = !code; continue; }
    if (!line.trim()) continue;
    if (line.startsWith('|')) {
      const group = [line]; while (i + 1 < lines.length && lines[i + 1].startsWith('|')) group.push(lines[++i]);
      result.push(table(group)); result.push(paragraph('')); continue;
    }
    const heading = /^(#{1,3})\s+(.+)$/.exec(line);
    if (heading) {
      result.push(paragraph(heading[2], { heading: [HeadingLevel.TITLE, HeadingLevel.HEADING_1, HeadingLevel.HEADING_2][heading[1].length - 1], keepNext: true }));
    } else if (code) {
      result.push(new Paragraph({ spacing: { after: 30 }, children: [new TextRun({ text: line, font: 'Consolas', size: 17 })] }));
    } else if (/^- /.test(line)) {
      result.push(paragraph(line.slice(2), { numbering: { reference: 'items', level: 0 } }));
    } else if (/^\d+\. /.test(line)) {
      result.push(paragraph(line.replace(/^\d+\. /, ''), { numbering: { reference: 'steps', level: 0 } }));
    } else result.push(paragraph(line.replace(/^>\s?/, '')));
  }
  return result;
}
async function main() {
  for (const name of ['race-implementation-plan-20261119', 'race-implementation-acceptance-20261119']) {
    const source = path.join(root, 'docs', 'engineering', name + '.md');
    const doc = new Document({
      creator: '军师请下令项目组', title: name,
      styles: {
        default: { document: { run: { font: 'Microsoft YaHei', size: 21, color: '203D33' } } },
        paragraphStyles: [
          { id: 'Title', name: 'Title', run: { font: 'Microsoft YaHei', size: 38, bold: true, color: '142E25' }, paragraph: { spacing: { after: 300 } } },
          { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true,
            run: { font: 'Microsoft YaHei', size: 29, bold: true }, paragraph: { spacing: { before: 260, after: 140 }, outlineLevel: 0 } },
          { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true,
            run: { font: 'Microsoft YaHei', size: 24, bold: true }, paragraph: { spacing: { before: 180, after: 100 }, outlineLevel: 1 } },
        ],
      },
      numbering: { config: [
        { reference: 'items', levels: [{ level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.LEFT,
          style: { paragraph: { indent: { left: 360, hanging: 240 } } } }] },
        { reference: 'steps', levels: [{ level: 0, format: LevelFormat.DECIMAL, text: '%1.', alignment: AlignmentType.LEFT,
          style: { paragraph: { indent: { left: 360, hanging: 240 } } } }] },
      ] },
      sections: [{
        properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1080, bottom: 1080, left: 1080, right: 1080 } } },
        headers: { default: new Header({ children: [paragraph('军师请下令  /  布阵竞速', { spacing: { after: 60 } })] }) },
        footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.RIGHT,
          children: [new TextRun('第 '), new TextRun({ children: [PageNumber.CURRENT] }), new TextRun(' 页')] })] }) },
        children: children(fs.readFileSync(source, 'utf8')),
      }],
    });
    const output = source.replace(/\.md$/, '.docx');
    fs.writeFileSync(output, await Packer.toBuffer(doc));
    process.stdout.write('已生成：' + output + '\n');
  }
}
main().catch(error => { process.stderr.write(String(error.stack || error)); process.exitCode = 1; });
