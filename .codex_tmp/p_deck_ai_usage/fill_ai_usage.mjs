import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const root = "G:/mirror_test/.codex_tmp/p_deck_ai_usage";
const starterPath = `${root}/template-starter.pptx`;
const outputPath = `${root}/candidate.pptx`;
const renderDir = `${root}/final-render`;
const layoutDir = `${root}/final-layout/final`;

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

  const cover = findRecord("textbox", 1, (item) => String(item.text || "").startsWith("答辩人：张聪"));
  presentation.resolve(cover.id).text.replace(cover.text, cover.text);

  const title = findRecord("textbox", 6, (item) => String(item.text || "").startsWith("3 . 项目组实习成果展示"));
  presentation.resolve(title.id).text.replace("3 . 项目组实习成果展示", "3 . AI 在游戏项目中的应用");

  const body = findRecord("textbox", 6, (item) => String(item.text || "").startsWith("在腾讯负责的整体工作情况"));
  const bodyShape = presentation.resolve(body.id);
  const replacements = [
    ["在腾讯负责的整体工作情况；", "【看得懂】AI 可执行的 PRD"],
    ["重点谈1~2个你认为最成功或最能体现个人专业能力的项目产出或成果；或1个你负责的比较遗憾或失败的项目；", "工具卡 PRD：写清目标、边界、规则、职责与验收，让 AI 按设计实现"],
    ["【小结与思考】在项目组实习过程中的总结，不仅限于", "【接得上】项目上下文管理"],
    ["过程中的挑战和难度在哪里，你如何克服的", "索引、大纲、专项文档与交接记录全部沉淀在项目仓库"],
    ["过程中你做过哪些重要的决策或抉择", "WorkBuddy 与 Codex 可以随时读档接手"],
    ["有哪些方法论/工具的应用或突破", "【做得出】AI 动画生产 Skill"],
    ["在工作中，你需要加强哪方面的能力", "固定骨架、根节点、比例与调色板，生成后完成对齐、验证、切片与 Unity 接入"],
  ];
  for (const [from, to] of replacements) bodyShape.text.replace(from, to);

  const notes = findRecord("notes", 6);
  presentation.resolve(notes.id).setText([
    "[Sources]",
    "- Docs/Design/镜前准备工具卡机制_PRD.md",
    "- Docs/README.md",
    "- Docs/Project/MirrorTrial_AI_Outline.md",
    "- Docs/Combat/CODEX_SKILL_EDITOR_HANDOFF.md",
    "- C:/Users/zharkzhang/.codex/skills/build-skeleton-consistent-pixel-animation/SKILL.md",
    "[/Sources]",
  ].join("\n"));

  const thanks = findRecord("textbox", 7, (item) => String(item.text || "") === "THANKS");
  presentation.resolve(thanks.id).text.replace(thanks.text, thanks.text);

  for (const [index, slide] of presentation.slides.items.entries()) {
    const stem = `slide-${String(index + 1).padStart(2, "0")}`;
    await writeBlob(path.join(renderDir, `${stem}.png`), await presentation.export({ slide, format: "png", scale: 1 }));
    const layout = await slide.export({ format: "layout" });
    await fs.writeFile(path.join(layoutDir, `${stem}.layout.json`), await layout.text(), "utf8");
  }

  await writeBlob(`${root}/final-montage.webp`, await presentation.export({ format: "webp", montage: true, scale: 1 }));
  const finalInspect = await presentation.inspect({
    kind: "slide,textbox,shape,image,notes,layout",
    maxChars: 200000,
  });
  await fs.writeFile(`${root}/final-inspect.ndjson`, finalInspect.ndjson || "", "utf8");

  const pptx = await PresentationFile.exportPptx(presentation);
  await pptx.save(outputPath);
  console.log(outputPath);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
