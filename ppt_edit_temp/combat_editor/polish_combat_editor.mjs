import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const root = "G:/mirror_test/ppt_edit_temp/combat_editor";
const inputPath = `${root}/candidate.pptx`;
const outputPath = `${root}/candidate-v2.pptx`;
const renderDir = `${root}/final-render-v2`;
const layoutDir = `${root}/final-layout-v2/final`;

async function writeBlob(filePath, blob) {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, new Uint8Array(await blob.arrayBuffer()));
}

const presentation = await PresentationFile.importPptx(await FileBlob.load(inputPath));
const snapshot = await presentation.inspect({ kind: "textbox", maxChars: 60000 });
const records = snapshot.ndjson.split(/\r?\n/).filter(Boolean).map(JSON.parse);
const titleRecord = records.find((x) => x.slide === 5 && x.kind === "textbox" && x.name === "combat-editor-title");
const bodyRecord = records.find((x) => x.slide === 5 && x.kind === "textbox" && x.name === "combat-editor-capabilities");
if (!titleRecord?.id || !bodyRecord?.id) throw new Error("Combat editor text boxes were not found");

const title = presentation.resolve(titleRecord.id);
title.position = { left: 74.81, top: 28, width: 1120, height: 72 };
title.text.style = {
  fontFamily: "Microsoft YaHei",
  fontSize: 40,
  bold: true,
  color: "#FFFFFF",
  verticalAlignment: "middle",
};

const body = presentation.resolve(bodyRecord.id);
body.position = { left: 74.81, top: 646, width: 1130, height: 44 };
body.text.style = {
  fontFamily: "Microsoft YaHei",
  fontSize: 21,
  color: "#FFFFFF",
  verticalAlignment: "middle",
};

for (const [index, slide] of presentation.slides.items.entries()) {
  const stem = `slide-${String(index + 1).padStart(2, "0")}`;
  await writeBlob(path.join(renderDir, `${stem}.png`), await presentation.export({ slide, format: "png", scale: 1 }));
  const layout = await slide.export({ format: "layout" });
  await fs.mkdir(layoutDir, { recursive: true });
  await fs.writeFile(path.join(layoutDir, `${stem}.layout.json`), await layout.text(), "utf8");
}
await writeBlob(`${root}/final-montage-v2.webp`, await presentation.export({ format: "webp", montage: true, scale: 1 }));
const finalInspect = await presentation.inspect({ kind: "slide,textbox,shape,image,notes,layout", maxChars: 240000 });
await fs.writeFile(`${root}/final-inspect-v2.ndjson`, finalInspect.ndjson || "", "utf8");
const pptx = await PresentationFile.exportPptx(presentation);
await pptx.save(outputPath);
console.log(outputPath);
