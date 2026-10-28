"""Build a contact sheet (grid montage) from an extracted frame sequence.

Usage:
    python tools/contact_sheet.py <frame_dir> <out.png> [--cols 5] [--labels]

Lets a whole outcome recording be reviewed in a single image.
"""

import argparse
import glob
import os

from PIL import Image, ImageDraw


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("frame_dir")
    parser.add_argument("out")
    parser.add_argument("--cols", type=int, default=5)
    parser.add_argument("--cell-width", type=int, default=320)
    parser.add_argument("--labels", action="store_true", help="stamp the file index on each cell")
    args = parser.parse_args()

    files = sorted(glob.glob(os.path.join(args.frame_dir, "*.png")))
    if not files:
        print(f"no frames in {args.frame_dir}")
        return 1

    cells = []
    for path in files:
        image = Image.open(path).convert("RGB")
        ratio = args.cell_width / image.width
        cells.append((path, image.resize((args.cell_width, int(image.height * ratio)))))

    cell_w = args.cell_width
    cell_h = cells[0][1].height
    rows = (len(cells) + args.cols - 1) // args.cols
    sheet = Image.new("RGB", (cols_w := args.cols * cell_w, rows * cell_h), "black")
    draw = ImageDraw.Draw(sheet)

    for i, (path, image) in enumerate(cells):
        x = (i % args.cols) * cell_w
        y = (i // args.cols) * cell_h
        sheet.paste(image, (x, y))
        draw.rectangle([x, y, x + cell_w - 1, y + cell_h - 1], outline="black")
        if args.labels:
            draw.text((x + 4, y + 4), os.path.basename(path).replace(".png", ""), fill="yellow")

    sheet.save(args.out)
    print(f"wrote {args.out}  ({len(cells)} cells, {sheet.width}x{sheet.height})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
