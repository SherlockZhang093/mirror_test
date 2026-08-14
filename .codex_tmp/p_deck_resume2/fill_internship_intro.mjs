import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const starterPath = "G:/mirror_test/.codex_tmp/p_deck_resume2/template-starter.pptx";
const outputPath = "G:/mirror_test/2026超新星P族答辩_张聪_实习经历版.pptx";
const renderDir = "G:/mirror_test/.codex_tmp/p_deck_resume2/final-render";
const layoutDir = "G:/mirror_test/.codex_tmp/p_deck_resume2/final-layout/final";

async function writeBlob(filePath, blob) {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, new Uint8Array(await blob.arrayBuffer()));
}

async function main() {
  await fs.mkdir(renderDir, { recursive: true });
  await fs.mkdir(layoutDir, { recursive: true });

  const presentation = await PresentationFile.importPptx(await FileBlob.load(starterPath));
  const snapshot = await presentation.inspect({ kind: "textbox,notes", maxChars: 100000 });
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
  presentation.resolve(coverRecord.id).text.replace(coverRecord.text, coverRecord.text);

  const introRecord = findRecord("textbox", 3, (item) => String(item.text || "").startsWith("清华大学｜互动媒体设计与技术硕士"));
  presentation.resolve(introRecord.id).text.replace(
    introRecord.text,
    [
      "清华大学｜互动媒体设计与技术硕士",
      "华中科技大学｜自动化学士",
      "Unity / UE · C++ / C# / Python",
      "",
      "莉莉丝｜Unity 工具技术策划",
      "• 推动关卡 / NPC 编辑器与动物玩法工具链落地",
      "• 建设资源检索、配置 ID 与对话内容生产流程",
      "",
      "腾讯 J1｜Unity 客户端 → UE 战斗技术策划",
      "• 负责 3C 镜头、物理交互与 UI 开发",
      "• 搭建英雄 Ability Pipeline，推进技能配置自动化",
    ].join("\n"),
  );

  const notesRecord = findRecord("notes", 3);
  presentation.resolve(notesRecord.id).setText([
    "[Sources]",
    "- 张聪_简历_游戏开发.pdf（用户提供，2页）",
    "[/Sources]",
  ].join("\n"));

  const appendixRecord = findRecord("textbox", 7, (item) => String(item.text || "") === "附：浅色背景");
  presentation.resolve(appendixRecord.id).text.replace(appendixRecord.text, appendixRecord.text);

  for (const [index, slide] of presentation.slides.items.entries()) {
    const stem = `slide-${String(index + 1).padStart(2, "0")}`;
    await writeBlob(path.join(renderDir, `${stem}.png`), await presentation.export({ slide, format: "png", scale: 1 }));
    const layout = await slide.export({ format: "layout" });
    await fs.writeFile(path.join(layoutDir, `${stem}.layout.json`), await layout.text(), "utf8");
  }

  await writeBlob(
    "G:/mirror_test/.codex_tmp/p_deck_resume2/final-montage.webp",
    await presentation.export({ format: "webp", montage: true, scale: 1 }),
  );

  const finalInspect = await presentation.inspect({
    kind: "slide,textbox,shape,image,notes,layout",
    maxChars: 200000,
  });
  await fs.writeFile(
    "G:/mirror_test/.codex_tmp/p_deck_resume2/final-inspect.ndjson",
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
