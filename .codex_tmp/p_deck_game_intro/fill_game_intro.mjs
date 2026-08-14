import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const root = "G:/mirror_test/.codex_tmp/p_deck_game_intro";
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

  const title = findRecord("textbox", 4, (item) => String(item.text || "").startsWith("2 . 命题研究展示"));
  presentation.resolve(title.id).text.replace(title.text, "2 . 游戏整体思路");

  const body = findRecord("textbox", 4, (item) => String(item.text || "").startsWith("按照题目要求"));
  const bodyShape = presentation.resolve(body.id);
  const replacements = [
    ["按照题目要求，完整呈现", "【游戏类型】2D 横版动作冒险"],
    ["其中【游戏提案】（游戏提案思路逻辑呈现；若有demo，需要安排评委游戏体验环节）", "类银河城式能力成长｜轻度 Roguelite｜《丝之歌》式高机动战斗参考"],
    ["【小结与思考】对命题研究的总结，不仅限于", "【核心玩法】现实层推进 → 镜像挑战 → 击败镜像自我 → 夺回能力"],
    ["阐述任务过程中遇到的挑战和难度，你如何克服的", "【成长路径】击败 Boss，永久解锁镜刃、冲刺等核心能力"],
    ["过程中你做过哪些重要的决策或抉择", "收集生命精华，关末选择生命、攻击或移速强化"],
    ["在命题研究中，你需要加强哪些方面的能力", "组合已有能力，挑战更强敌人与最终 Boss"],
  ];
  for (const [from, to] of replacements) bodyShape.text.replace(from, to);

  const notes = findRecord("notes", 4);
  presentation.resolve(notes.id).setText([
    "[Sources]",
    "- Docs/Project/MirrorTrial_iWiki_Rewrite.md",
    "- Docs/Project/MirrorTrial_ImplementationPlan.md",
    "- Docs/Level/生命精华系统_完整实现说明.md",
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
