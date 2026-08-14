import { FileBlob, PresentationFile } from "@oai/artifact-tool";
const p = await PresentationFile.importPptx(await FileBlob.load("G:/mirror_test/ppt_edit_temp/combat_editor/candidate.pptx"));
const s = await p.inspect({ kind: "image,textbox", maxChars: 30000 });
const rs = s.ndjson.split(/\r?\n/).filter(Boolean).map(JSON.parse).filter((x) => x.slide === 5);
for (const r of rs) {
  const o = p.resolve(r.id);
  console.log(r.kind, r.id, "own", Object.keys(o), "data", Object.keys(o.data || {}), JSON.stringify(o.data || {}).slice(0, 1200));
}
const slide = p.slides.getItem(4);
console.log("slide data keys", Object.keys(slide.data || {}));
console.log(JSON.stringify(slide.toProto()).slice(0, 10000));
