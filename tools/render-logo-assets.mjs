// Rasterizes assets/branding/hanki-logo.svg (and the small-size-optimized
// hanki-logo-small.svg) into every PNG size the branding set needs.
//
// This is a one-off local dev tool, not part of the app build:
//   cd tools && npm install sharp   (only if node_modules/sharp isn't present)
//   node tools/render-logo-assets.mjs
//
// Only paths relative to the repo root are used -- run it from the repo root.
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import sharp from "sharp";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const brandingDir = path.join(repoRoot, "assets", "branding");

const fullSvg = readFileSync(path.join(brandingDir, "hanki-logo.svg"));
const smallSvg = readFileSync(path.join(brandingDir, "hanki-logo-small.svg"));

// Sizes 48px and below use the small-icon-optimized artwork (thicker H,
// flat fill, simplified sparkle) so it stays legible in the Windows tray.
const targets = [1024, 512, 256, 128, 64, 48, 40, 32, 24, 20, 16];

function labelSvg(text, width = 180, height = 38, color = "#334155", fontSize = 22) {
  return Buffer.from(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
      <text x="${width / 2}" y="${Math.round(height * 0.72)}"
            text-anchor="middle" font-family="Segoe UI, Arial, sans-serif"
            font-size="${fontSize}" font-weight="600" fill="${color}">${text}</text>
    </svg>`,
  );
}

async function createContactSheet() {
  const sheetWidth = 1800;
  const sheetHeight = 1420;
  const panelX = 40;
  const panelWidth = sheetWidth - panelX * 2;
  const panels = [
    { y: 100, color: "#F8FAFC", label: "밝은 배경", text: "#334155" },
    { y: 560, color: "#111827", label: "어두운 배경", text: "#E2E8F0" },
  ];
  const samples = [
    { source: 1024, display: 256, label: "1024px (25%)" },
    { source: 256, display: 256, label: "256px" },
    { source: 128, display: 128, label: "128px" },
    { source: 64, display: 64, label: "64px" },
    { source: 48, display: 48, label: "48px" },
    { source: 32, display: 32, label: "32px" },
    { source: 24, display: 24, label: "24px" },
    { source: 20, display: 20, label: "20px" },
    { source: 16, display: 16, label: "16px" },
  ];
  const gap = 34;
  const samplesWidth =
    samples.reduce((sum, sample) => sum + sample.display, 0) + gap * (samples.length - 1);
  const composites = [
    {
      input: labelSvg("한키 로고 크기·배경 검토 시트", 760, 56, "#0F172A", 34),
      left: 70,
      top: 26,
    },
  ];

  for (const panel of panels) {
    composites.push({
      input: {
        create: {
          width: panelWidth,
          height: 410,
          channels: 4,
          background: panel.color,
        },
      },
      left: panelX,
      top: panel.y,
    });
    composites.push({
      input: labelSvg(panel.label, 220, 46, panel.text, 25),
      left: 70,
      top: panel.y + 18,
    });

    let x = panelX + Math.round((panelWidth - samplesWidth) / 2);
    for (const sample of samples) {
      const sourcePath = path.join(brandingDir, `hanki-logo-${sample.source}.png`);
      const buffer = await sharp(sourcePath)
        .resize(sample.display, sample.display, { kernel: sharp.kernel.nearest })
        .png()
        .toBuffer();
      composites.push({
        input: buffer,
        left: x,
        top: panel.y + 82 + Math.round((256 - sample.display) / 2),
      });
      composites.push({
        input: labelSvg(sample.label, Math.max(100, sample.display + 50), 34, panel.text, 18),
        left: x - Math.round((Math.max(100, sample.display + 50) - sample.display) / 2),
        top: panel.y + 350,
      });
      x += sample.display + gap;
    }
  }

  composites.push({
    input: labelSvg("작은 아이콘 확대 미리보기 (최근접 8×)", 650, 52, "#0F172A", 28),
    left: 70,
    top: 1000,
  });
  const enlarged = [64, 32, 24, 20, 16];
  let enlargedX = 80;
  for (const size of enlarged) {
    const display = size * (size >= 32 ? 4 : 8);
    const buffer = await sharp(path.join(brandingDir, `hanki-logo-${size}.png`))
      .resize(display, display, { kernel: sharp.kernel.nearest })
      .png()
      .toBuffer();
    composites.push({ input: buffer, left: enlargedX, top: 1080 });
    composites.push({
      input: labelSvg(`${size}px → ${display}px`, display, 38, "#334155", 19),
      left: enlargedX,
      top: 1085 + display,
    });
    enlargedX += display + 58;
  }

  await sharp({
    create: {
      width: sheetWidth,
      height: sheetHeight,
      channels: 4,
      background: "#E2E8F0",
    },
  })
    .composite(composites)
    .png()
    .toFile(path.join(brandingDir, "hanki-logo-contact-sheet.png"));
  console.log("wrote assets/branding/hanki-logo-contact-sheet.png");
}

async function createInstallerArtwork() {
  const wizard = await sharp({
    create: {
      width: 164,
      height: 314,
      channels: 4,
      background: "#F6F9FF",
    },
  })
    .composite([
      {
        input: await sharp(fullSvg, { density: 384 }).resize(132, 132).png().toBuffer(),
        left: 16,
        top: 32,
      },
      {
        input: labelSvg("한키", 140, 42, "#174A8B", 26),
        left: 12,
        top: 184,
      },
      {
        input: labelSvg("긴 문장을 한 번에", 150, 32, "#3977A8", 15),
        left: 7,
        top: 226,
      },
    ])
    .png()
    .toBuffer();
  await sharp(wizard).toFile(path.join(brandingDir, "hanki-installer-wizard.png"));

  await sharp({
    create: {
      width: 55,
      height: 55,
      channels: 4,
      background: "#F6F9FF",
    },
  })
    .composite([
      {
        input: await sharp(smallSvg, { density: 384 }).resize(49, 49).png().toBuffer(),
        left: 3,
        top: 3,
      },
    ])
    .png()
    .toFile(path.join(brandingDir, "hanki-installer-small.png"));
  console.log("wrote installer wizard artwork");
}

async function main() {
  for (const size of targets) {
    const source = size <= 48 ? smallSvg : fullSvg;
    const outFile = path.join(brandingDir, `hanki-logo-${size}.png`);
    await sharp(source, { density: Math.max(96, Math.round((size / 1024) * 96 * 8)) })
      .resize(size, size, { kernel: sharp.kernel.lanczos3 })
      .png()
      .toFile(outFile);
    console.log(`wrote ${path.relative(repoRoot, outFile)}`);
  }

  // Convenience 512px "plain" copy requested alongside the sized set.
  await sharp(fullSvg, { density: 384 })
    .resize(512, 512, { kernel: sharp.kernel.lanczos3 })
    .png()
    .toFile(path.join(brandingDir, "hanki-logo.png"));
  console.log("wrote assets/branding/hanki-logo.png (512px)");

  // 1024px full-size preview for visual QA.
  await sharp(fullSvg, { density: 384 })
    .resize(1024, 1024, { kernel: sharp.kernel.lanczos3 })
    .png()
    .toFile(path.join(brandingDir, "hanki-logo-preview.png"));
  console.log("wrote assets/branding/hanki-logo-preview.png");

  await createContactSheet();
  await createInstallerArtwork();
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
