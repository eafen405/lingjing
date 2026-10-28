"""Extract frames from a Unity outcome recording for visual verification.

Usage:
    python tools/extract_frames.py <video> <out_dir> [--every 1.0] [--count 24]

The challenge packages ship an "Outcome" movie showing the finished game, which
is the only authoritative reference for the expected pacing (how fast the dog
runs relative to the falling ball, where objects leave the screen, ...).
"""

import argparse
import os
import sys


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("video")
    parser.add_argument("out_dir")
    parser.add_argument("--every", type=float, default=1.0, help="seconds between frames")
    parser.add_argument("--count", type=int, default=24, help="max frames")
    parser.add_argument("--width", type=int, default=960)
    args = parser.parse_args()

    try:
        import av  # PyAV
    except ImportError:
        print("PyAV is required: pip install av", file=sys.stderr)
        return 2

    os.makedirs(args.out_dir, exist_ok=True)
    container = av.open(args.video)
    stream = container.streams.video[0]
    duration = float(stream.duration * stream.time_base) if stream.duration else 0.0
    if not duration:
        duration = container.duration / av.time_base

    fps = float(stream.average_rate) if stream.average_rate else 30.0
    print(f"video: {args.video}")
    print(f"duration: {duration:.2f}s  fps: {fps:.2f}  size: {stream.width}x{stream.height}")

    step = max(1, int(round(args.every * fps)))
    written = 0
    for index, frame in enumerate(container.decode(video=0)):
        if index % step:
            continue
        if written >= args.count:
            break
        out = os.path.join(args.out_dir, f"frame_{index:05d}.png")
        image = frame.to_image()
        if args.width and image.width != args.width:
            ratio = args.width / image.width
            image = image.resize((args.width, int(image.height * ratio)))
        image.save(out)
        written += 1
    container.close()
    print(f"wrote {written} frames to {args.out_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
