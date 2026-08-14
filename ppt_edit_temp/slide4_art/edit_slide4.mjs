import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const root = "G:/mirror_test/ppt_edit_temp/slide4_art";
const starterPath = `${root}/template-starter.pptx`;
const outputPath = `${root}/final.pptx`;
const imagePath = `${root}/assets/mirror-loop-concept.png`;
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
const snapshot = await presentation.inspect({
  kind: "slide,textbox,image,notes",
  maxChars: 120000,
});
const records = snapshot.ndjson.split(/\r?\n/).filter(Boolean).map(JSON.parse);

const find = (kind, slide, predicate = () => true) => {
  const record = records.find((item) => item.kind === kind && item.slide === slide && predicate(item));
  if (!record?.id) throw new Error(`Missing ${kind} on slide ${slide}`);
  return record;
};

// Preserve the two inherited structural placeholders that the template validator
// requires to be explicitly handled.
const cover = find("textbox", 1, (item) => String(item.text || "").includes("答辩人：张聪"));
presentation.resolve(cover.id).text.replace(cover.text, cover.text);
const thanks = find("textbox", 7, (item) => String(item.text || "") === "THANKS");
presentation.resolve(thanks.id).text.replace(thanks.text, thanks.text);

const backgroundRecord = find("image", 4, (item) => Array.isArray(item.bbox) && item.bbox[2] >= 1200);
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
  blob: await readImageBlob(imagePath),
  contentType: "image/png",
  alt: "原创像素风概念图：现实世界与镜像世界之间的主角对决",
  ...(fit ? { fit } : { fit: "cover" }),
  prompt: "16:9 pixel-art concept art showing a ruined real world, fractured mirror realm, protagonist confronting a mirrored self, and left-side negative space for slide copy.",
});
background.frame = frame;
background.crop = crop;
background.geometry = geometry;
background.borderRadius = borderRadius;
background.rotation = rotation;
background.flipHorizontal = flipHorizontal;
background.flipVertical = flipVertical;
background.lockAspectRatio = lockAspectRatio;

const bodyRecord = find("textbox", 4, (item) => String(item.text || "").includes("【游戏类型】"));
const body = presentation.resolve(bodyRecord.id);
body.text = [
  "【游戏类型】2D 横版动作冒险",
  "类银河城成长 × 轻度 Roguelite × 高机动战斗",
  "",
  "【核心玩法】",
  "现实推进 → 镜像挑战 → 击败自我 → 夺回能力",
  "",
  "【成长路径】",
  "Boss 解锁核心能力 → 关末强化 → 组合能力挑战终局",
].join("\n");
body.position = { left: 94.26, top: 142, width: 548, height: 408 };
body.text.style = {
  fontFamily: "Microsoft YaHei",
  fontSize: 25,
  color: "#FFFFFF",
  verticalAlignment: "top",
};

const notesRecord = find("notes", 4);
const notes = presentation.resolve(notesRecord.id);
const existingNotes = String(notesRecord.text || "").trim();
notes.setText([
  existingNotes,
  "[Sources]",
  "- AI-generated original illustration: assets/mirror-loop-concept.png",
  "- Generated with the built-in image generation tool; prompt recorded in source-notes.txt",
  "[/Sources]",
].filter(Boolean).join("\n"));

for (const [index, slide] of presentation.slides.items.entries()) {
  const stem = `slide-${String(index + 1).padStart(2, "0")}`;
  await writeBlob(path.join(renderDir, `${stem}.png`), await presentation.export({ slide, format: "png", scale: 1 }));
  const layout = await slide.export({ format: "layout" });
  await fs.mkdir(layoutDir, { recursive: true });
  await fs.writeFile(path.join(layoutDir, `${stem}.layout.json`), await layout.text(), "utf8");
}

await writeBlob(`${root}/final-montage.webp`, await presentation.export({ format: "webp", montage: true, scale: 1 }));
const finalInspect = await presentation.inspect({
  kind: "slide,textbox,shape,image,notes,layout",
  maxChars: 240000,
});
await fs.writeFile(`${root}/final-inspect.ndjson`, finalInspect.ndjson || "", "utf8");

const pptx = await PresentationFile.exportPptx(presentation);
await pptx.save(outputPath);
console.log(outputPath);
