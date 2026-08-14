import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const starterPath = "G:/mirror_test/.codex_tmp/p_deck_resume/template-starter.pptx";
const outputPath = "G:/mirror_test/2026超新星P族答辩_张聪_简历版.pptx";
const renderDir = "G:/mirror_test/.codex_tmp/p_deck_resume/final-render";
const layoutDir = "G:/mirror_test/.codex_tmp/p_deck_resume/final-layout/final";

async function writeBlob(filePath, blob) {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, new Uint8Array(await blob.arrayBuffer()));
}

async function main() {
  await fs.mkdir(renderDir, { recursive: true });
  await fs.mkdir(layoutDir, { recursive: true });

  const presentation = await PresentationFile.importPptx(await FileBlob.load(starterPath));
  const snapshot = await presentation.inspect({
    kind: "textbox,notes",
    maxChars: 100000,
  });
  const records = (snapshot.ndjson || "")
    .split(/\r?\n/)
    .filter(Boolean)
    .map((line) => JSON.parse(line));

  const findRecord = (kind, slide, predicate) => {
    const record = records.find(
      (item) => item.kind === kind && item.slide === slide && (!predicate || predicate(item)),
    );
    if (!record?.id) throw new Error(`Missing ${kind} on slide ${slide}`);
    return record;
  };

  const coverRecord = findRecord("textbox", 1, (item) => String(item.text || "").startsWith("答辩人：张聪"));
  const cover = presentation.resolve(coverRecord.id);
  cover.text.replace(coverRecord.text, coverRecord.text);

  const introRecord = findRecord("textbox", 3, (item) => String(item.text || "").startsWith("清华大学｜互动媒体设计与技术"));
  const intro = presentation.resolve(introRecord.id);
  intro.text.replace(
    introRecord.text,
    [
      "清华大学｜互动媒体设计与技术硕士",
      "华中科技大学｜自动化学士",
      "Unity / UE · C++ / C# / Python",
      "",
      "当前：火影忍者项目组｜Unity 战斗技术策划",
      "经历：腾讯 J1 客户端与 UE 战斗技术策划｜莉莉丝工具技术策划",
      "方向：战斗系统 · 技能配置自动化 · 策划工具链 · 3C 交互",
      "",
      "研究：感知与反馈融合的交互设备",
      "成果：IROS 2025 一作｜发明专利申请",
    ].join("\n"),
  );

  const notesRecord = findRecord("notes", 3);
  const notes = presentation.resolve(notesRecord.id);
  notes.setText([
    "[Sources]",
    "- 张聪_简历_游戏开发.pdf（用户提供，2页）",
    "[/Sources]",
  ].join("\n"));

  const appendixRecord = findRecord("textbox", 7, (item) => String(item.text || "") === "附：浅色背景");
  const appendix = presentation.resolve(appendixRecord.id);
  appendix.text.replace(appendixRecord.text, appendixRecord.text);

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
  await writeBlob("G:/mirror_test/.codex_tmp/p_deck_resume/final-montage.webp", montage);

  const finalInspect = await presentation.inspect({
    kind: "slide,textbox,shape,image,notes,layout",
    maxChars: 200000,
  });
  await fs.writeFile(
    "G:/mirror_test/.codex_tmp/p_deck_resume/final-inspect.ndjson",
    finalInspect.ndjson || "",
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
