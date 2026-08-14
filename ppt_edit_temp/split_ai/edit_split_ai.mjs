import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const root = "G:/mirror_test/ppt_edit_temp/split_ai";
const starterPath = `${root}/template-starter.pptx`;
const outputPath = `${root}/final.pptx`;
const backgroundPath = `${root}/assets/ai-section-background.jpeg`;
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

const presentation = await PresentationFile.importPptx(await FileBlob.load(starterPath));
const snapshot = await presentation.inspect({ kind: "slide,textbox,image,notes", maxChars: 160000 });
const records = snapshot.ndjson.split(/\r?\n/).filter(Boolean).map(JSON.parse);

function find(kind, slide, predicate = () => true) {
  const record = records.find((item) => item.kind === kind && item.slide === slide && predicate(item));
  if (!record?.id) throw new Error(`Missing ${kind} on slide ${slide}`);
  return record;
}

// Explicitly preserve the inherited structural placeholders required by the template.
const cover = find("textbox", 1, (item) => String(item.text || "").includes("答辩人：张聪"));
presentation.resolve(cover.id).text.replace(cover.text, cover.text);
const thanks = find("textbox", 9, (item) => String(item.text || "") === "THANKS");
presentation.resolve(thanks.id).text.replace(thanks.text, thanks.text);

const pages = [
  {
    slide: 6,
    title: "3.1 写给 AI 看的 PRD",
    body: [
      "以工具卡系统为例",
      "把“想做什么”写成“如何执行、如何验收”",
      "",
      "• 配置字段：卡牌类型、数值、触发时机",
      "• 规则边界：互斥、叠加、异常处理",
      "• 验收标准：输入什么、应该看到什么",
    ].join("\n"),
    notes: [
      "[Sources]",
      "- Docs/Design/镜前准备工具卡机制_PRD.md",
      "[/Sources]",
      "讲解重点：用工具卡系统展示我如何把策划描述拆成字段、规则、边界和可复现的验收条件。",
    ].join("\n"),
  },
  {
    slide: 7,
    title: "3.2 让 AI 随时接上项目",
    body: [
      "上下文不留在聊天里，而是沉淀进仓库",
      "",
      "• 索引：先读哪些文档",
      "• 专项文档：系统现在怎么工作",
      "• 交接记录：做到哪里、下一步是什么",
      "",
      "WorkBuddy 与 Codex 读取同一套上下文",
    ].join("\n"),
    notes: [
      "[Sources]",
      "- Docs/README.md",
      "- Docs/Project/MirrorTrial_AI_Outline.md",
      "- Docs/Combat/CODEX_SKILL_EDITOR_HANDOFF.md",
      "[/Sources]",
      "讲解重点：索引、大纲、专项文档和交接记录共同构成可读取的项目上下文，使 WorkBuddy 与 Codex 可以来回接手。",
    ].join("\n"),
  },
  {
    slide: 8,
    title: "3.3 把 AI 动画真正接进 Unity",
    body: [
      "把稳定生产流程封装成 Skill",
      "",
      "• 固定骨架、根节点、比例与调色板",
      "• 生成 → 对齐 → 验证 → 切片",
      "• 重建 AnimationClip 并接入 Unity",
    ].join("\n"),
    notes: [
      "[Sources]",
      "- C:/Users/zharkzhang/.codex/skills/build-skeleton-consistent-pixel-animation/SKILL.md",
      "- C:/Users/zharkzhang/.codex/skills/task-animation-generation/SKILL.md",
      "[/Sources]",
      "讲解重点：不只生成动画图片，而是用 Skill 固定骨架、根节点、尺度和调色板，再完成验证、切片和 Unity 接入。",
    ].join("\n"),
  },
];

const backgroundBytes = await readImageBlob(backgroundPath);

for (const page of pages) {
  const titleRecord = find("textbox", page.slide, (item) => String(item.name || "").includes("文本框 2"));
  const title = presentation.resolve(titleRecord.id);
  title.text = page.title;
  title.text.style = {
    fontFamily: "Tencent Sans W7",
    fontSize: 42.67,
    bold: true,
    color: "#FFFFFF",
    verticalAlignment: "middle",
  };
  title.position = { left: 74.81, top: 35.23, width: 1125, height: 95 };

  const bodyRecord = find("textbox", page.slide, (item) => String(item.name || "").includes("文本框 1"));
  const body = presentation.resolve(bodyRecord.id);
  body.text = page.body;
  body.text.style = {
    fontFamily: "Microsoft YaHei",
    fontSize: 26.67,
    color: "#FFFFFF",
    verticalAlignment: "top",
  };
  body.position = { left: 74.81, top: 166, width: 500, height: 398 };

  const backgroundRecord = find("image", page.slide, (item) => Array.isArray(item.bbox) && item.bbox[2] >= 1200);
  const background = presentation.resolve(backgroundRecord.id);
  const frame = background.frame;
  const crop = background.crop;
  const fit = background.fit;
  const geometry = background.geometry;
  const borderRadius = background.borderRadius;
  const rotation = background.rotation;
  const flipHorizontal = background.flipHorizontal;
  const flipVertical = background.flipVertical;
  const lockAspectRatio = background.lockAspectRatio;
  background.replace({
    blob: backgroundBytes,
    contentType: "image/jpeg",
    alt: "AI 应用章节深色背景",
    ...(fit ? { fit } : { fit: "cover" }),
  });
  background.frame = frame;
  background.crop = crop;
  background.geometry = geometry;
  background.borderRadius = borderRadius;
  background.rotation = rotation;
  background.flipHorizontal = flipHorizontal;
  background.flipVertical = flipVertical;
  background.lockAspectRatio = lockAspectRatio;

  const oldImageRecord = find("image", page.slide, (item) => Array.isArray(item.bbox) && item.bbox[2] < 800);
  presentation.resolve(oldImageRecord.id).delete();

  const notesRecord = find("notes", page.slide);
  presentation.resolve(notesRecord.id).setText(page.notes);
}

for (const [index, slide] of presentation.slides.items.entries()) {
  const stem = `slide-${String(index + 1).padStart(2, "0")}`;
  await writeBlob(path.join(renderDir, `${stem}.png`), await presentation.export({ slide, format: "png", scale: 1 }));
  const layout = await slide.export({ format: "layout" });
  await fs.mkdir(layoutDir, { recursive: true });
  await fs.writeFile(path.join(layoutDir, `${stem}.layout.json`), await layout.text(), "utf8");
}

await writeBlob(`${root}/final-montage.webp`, await presentation.export({ format: "webp", montage: true, scale: 1 }));
const finalInspect = await presentation.inspect({ kind: "slide,textbox,shape,image,notes,layout", maxChars: 260000 });
await fs.writeFile(`${root}/final-inspect.ndjson`, finalInspect.ndjson || "", "utf8");
const pptx = await PresentationFile.exportPptx(presentation);
await pptx.save(outputPath);
console.log(outputPath);
