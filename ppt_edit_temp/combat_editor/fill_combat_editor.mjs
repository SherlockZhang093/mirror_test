import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const root = "G:/mirror_test/ppt_edit_temp/combat_editor";
const starterPath = `${root}/template-starter.pptx`;
const outputPath = `${root}/candidate.pptx`;
const screenshotPath = `${root}/assets/combat-editor.png`;
const renderDir = `${root}/final-render`;
const layoutDir = `${root}/final-layout/final`;

async function writeBlob(filePath, blob) {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, new Uint8Array(await blob.arrayBuffer()));
}

async function readImageBlob(filePath) {
  const bytes = await fs.readFile(filePath);
  return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
}

async function main() {
  const presentation = await PresentationFile.importPptx(await FileBlob.load(starterPath));
  const snapshot = await presentation.inspect({ kind: "textbox,image,notes", maxChars: 120000 });
  const records = (snapshot.ndjson || "").split(/\r?\n/).filter(Boolean).map(JSON.parse);

  const findRecord = (kind, slide, predicate) => {
    const record = records.find(
      (item) => item.kind === kind && item.slide === slide && (!predicate || predicate(item)),
    );
    if (!record?.id) throw new Error(`Missing ${kind} on slide ${slide}`);
    return record;
  };

  const cover = findRecord("textbox", 1, (item) => String(item.text || "").startsWith("答辩人：张聪"));
  presentation.resolve(cover.id).text.replace(cover.text, cover.text);

  const editorTitle = findRecord("textbox", 5, (item) => String(item.text || "").startsWith("3 . AI 在游戏项目中的应用"));
  presentation.resolve(editorTitle.id).text.replace("3 . AI 在游戏项目中的应用", "战斗编辑器：连招、参数、预览一体化");

  const editorBody = findRecord("textbox", 5, (item) => String(item.text || "").startsWith("【看得懂】AI 可执行的 PRD"));
  const bodyShape = presentation.resolve(editorBody.id);
  const bodyReplacements = [
    ["【看得懂】AI 可执行的 PRD", "节点编排连招｜逐帧配置判定与反馈｜TestGym 运行时预览"],
    ["工具卡 PRD：写清目标、边界、规则、职责与验收，让 AI 按设计实现", ""],
    ["【接得上】项目上下文管理", ""],
    ["索引、大纲、专项文档与交接记录全部沉淀在项目仓库", ""],
    ["WorkBuddy 与 Codex 可以随时读档接手", ""],
    ["【做得出】AI 动画生产 Skill", ""],
    ["固定骨架、根节点、比例与调色板，生成后完成对齐、验证、切片与 Unity 接入", ""],
  ];
  for (const [from, to] of bodyReplacements) bodyShape.text.replace(from, to);
  bodyShape.position = { left: 74.81, top: 632, width: 1130, height: 62 };

  const backgroundRecord = findRecord("image", 5, (item) => Array.isArray(item.bbox) && item.bbox[2] >= 1200);
  const background = presentation.resolve(backgroundRecord.id);
  const oldFrame = background.frame;
  const oldFit = background.fit;
  background.delete();
  const editorSlide = presentation.slides.getItem(4);
  const replacement = editorSlide.images.add({
    blob: await readImageBlob(screenshotPath),
    contentType: "image/png",
    alt: "Unity 战斗编辑器：连招路线图、招式参数和逐帧攻击框配置",
    fit: oldFit || "cover",
    position: oldFrame,
  });

  // Re-add inherited text after the replacement image so it remains above the screenshot.
  const titleText = "战斗编辑器：连招、参数、预览一体化";
  const capabilityText = "节点编排连招｜逐帧配置判定与反馈｜TestGym 运行时预览";
  const titleShape = presentation.resolve(editorTitle.id);
  const titlePosition = titleShape.position;
  const titleStyle = titleShape.text.style;
  const bodyPosition = bodyShape.position;
  const bodyStyle = bodyShape.text.style;
  titleShape.delete();
  bodyShape.delete();
  const titleOverlay = editorSlide.shapes.add({
    geometry: "textbox",
    name: "combat-editor-title",
    position: titlePosition,
    fill: "none",
    line: { style: "solid", fill: "none", width: 0 },
  });
  titleOverlay.text = titleText;
  titleOverlay.text.style = titleStyle;
  const bodyOverlay = editorSlide.shapes.add({
    geometry: "textbox",
    name: "combat-editor-capabilities",
    position: bodyPosition,
    fill: "none",
    line: { style: "solid", fill: "none", width: 0 },
  });
  bodyOverlay.text = capabilityText;
  bodyOverlay.text.style = bodyStyle;

  const notes = findRecord("notes", 5);
  presentation.resolve(notes.id).setText([
    "[Sources]",
    "- 用户提供：codex-clipboard-3af23637-e6c0-47fe-ab61-bc9a3b1338bc.png",
    "- Docs/Combat/CODEX_SKILL_EDITOR_HANDOFF.md",
    "[/Sources]",
    "讲解要点：",
    "1. 连招图：招式作为节点，点按、长按、松开与状态条件作为连线。",
    "2. 招式配置：动画、前摇/有效/后摇、攻击框、伤害、击退、蓄力和命中反馈统一配置。",
    "3. 运行时预览：TestGym 模拟输入、同步编辑数据，并高亮当前节点与连线。",
    "4. 核心价值：策划可以直接调配置并验证战斗手感，减少反复修改代码。",
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
  const finalInspect = await presentation.inspect({ kind: "slide,textbox,shape,image,notes,layout", maxChars: 240000 });
  await fs.writeFile(`${root}/final-inspect.ndjson`, finalInspect.ndjson || "", "utf8");
  const pptx = await PresentationFile.exportPptx(presentation);
  await pptx.save(outputPath);
  console.log(outputPath);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
