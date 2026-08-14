import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const starterPath = "G:/mirror_test/.codex_tmp/p_deck_audit/template-starter.pptx";
const outputPath = "G:/mirror_test/2026超新星P族答辩_张聪_初稿.pptx";
const renderDir = "G:/mirror_test/.codex_tmp/p_deck_audit/final-render";
const layoutDir = "G:/mirror_test/.codex_tmp/p_deck_audit/final-layout/final";

async function writeBlob(filePath, blob) {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, new Uint8Array(await blob.arrayBuffer()));
}

async function main() {
  await fs.mkdir(renderDir, { recursive: true });
  await fs.mkdir(layoutDir, { recursive: true });

  const presentation = await PresentationFile.importPptx(await FileBlob.load(starterPath));

  const before = await presentation.inspect({
    kind: "textbox",
    maxChars: 50000,
  });
  const records = (before.ndjson || "")
    .split(/\r?\n/)
    .filter(Boolean)
    .map((line) => JSON.parse(line));
  const findTextbox = (slide, textStart) => {
    const record = records.find(
      (item) => item.kind === "textbox" && item.slide === slide && String(item.text || "").startsWith(textStart),
    );
    if (!record?.id) throw new Error(`Missing textbox on slide ${slide}: ${textStart}`);
    return presentation.resolve(record.id);
  };

  const cover = findTextbox(1, "答辩人：sanzhang");
  cover.text.replace("sanzhang(张三)", "张聪");
  cover.text.replace("岗位：", "岗位：技术策划");
  cover.text.replace("实习小组：XX部门/XX中心/XX组", "实习小组：火影忍者项目组");
  cover.text.replace("导师：", "导师：phynoluo（罗斯颖）");
  cover.text.replace("直接leader:", "直接leader：loynliu（刘荃）");
  cover.text.replace("入职时间：", "入职时间：2026-06-09");

  const introduction = findTextbox(3, "可简单介绍个人基本信息");
  introduction.text.replace(
    "可简单介绍个人基本信息（毕业学校/专业信息等）、兴趣、关注领域及过往非腾讯实习经历等，请挑选其中最主要的列示（建议1页）\n",
    "清华大学｜互动媒体设计与技术\n火影忍者项目组｜技术策划\n\n2026 年 6 月加入腾讯游戏\n在导师 phynoluo（罗斯颖）与 Leader " + "loynliu（刘荃）的指导下开展实习工作",
  );

  const appendixTitle = findTextbox(7, "附：浅色背景");
  appendixTitle.text.replace("附：浅色背景", "附：浅色背景");

  for (const [index, slide] of presentation.slides.items.entries()) {
    const stem = `slide-${String(index + 1).padStart(2, "0")}`;
    await writeBlob(
      path.join(renderDir, `${stem}.png`),
      await presentation.export({ slide, format: "png", scale: 1 }),
    );
    const layout = await slide.export({ format: "layout" });
    await fs.writeFile(path.join(layoutDir, `${stem}.layout.json`), await layout.text(), "utf8");
  }

  const montage = await presentation.export({ format: "webp", montage: true, scale: 1 });
  await writeBlob("G:/mirror_test/.codex_tmp/p_deck_audit/final-montage.webp", montage);

  const inspect = await presentation.inspect({
    kind: "slide,textbox,shape,image,notes,layout",
    maxChars: 200000,
  });
  await fs.writeFile(
    "G:/mirror_test/.codex_tmp/p_deck_audit/final-inspect.ndjson",
    inspect.ndjson || "",
    "utf8",
  );

  const pptx = await PresentationFile.exportPptx(presentation);
  await pptx.save(outputPath);
  console.log(outputPath);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
