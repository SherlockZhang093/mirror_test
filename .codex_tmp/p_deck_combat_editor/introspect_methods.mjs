import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const presentation = await PresentationFile.importPptx(
  await FileBlob.load("G:/mirror_test/.codex_tmp/p_deck_combat_editor/template-starter.pptx"),
);
const snapshot = await presentation.inspect({ kind: "image,slide", maxChars: 20000 });
const records = snapshot.ndjson.split(/\r?\n/).filter(Boolean).map(JSON.parse);
const imageRecord = records.find((item) => item.kind === "image" && item.slide === 5);
const image = presentation.resolve(imageRecord.id);
const slide = presentation.slides.getItem(4);
console.log("image own", Object.getOwnPropertyNames(image));
console.log("image proto", Object.getOwnPropertyNames(Object.getPrototypeOf(image)));
console.log("images own", Object.getOwnPropertyNames(slide.images));
console.log("images proto", Object.getOwnPropertyNames(Object.getPrototypeOf(slide.images)));
console.log("shapes proto", Object.getOwnPropertyNames(Object.getPrototypeOf(slide.shapes)));
console.log("help", presentation.help("*", { search: "remove delete image shape collection", include: ["index", "notes"], maxChars: 8000 }));
