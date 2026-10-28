"""Turn a folder of PNG frames into a video.

Usage:
    python tools/frames-to-video.py <frames_dir> <output.mp4> [--fps 30]

Used together with the in-Unity recorder (tools/ScreenRecorder.cs). PyAV is used
because ffmpeg is not installed on this machine.
"""

import argparse
import os
import sys
from fractions import Fraction


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("frames_dir")
    parser.add_argument("output")
    parser.add_argument("--fps", type=int, default=30)
    args = parser.parse_args()

    try:
        import av
    except ImportError:
        print("PyAV is required: pip install av", file=sys.stderr)
        return 2

    frames = sorted(f for f in os.listdir(args.frames_dir) if f.lower().endswith(".png"))
    if not frames:
        print(f"no frames found in {args.frames_dir}", file=sys.stderr)
        return 1

    first = os.path.join(args.frames_dir, frames[0])
    with av.open(first) as probe:
        stream = probe.streams.video[0]
        width, height = stream.codec_context.width, stream.codec_context.height

    os.makedirs(os.path.dirname(os.path.abspath(args.output)), exist_ok=True)

    time_base = Fraction(1, args.fps)

    with av.open(args.output, mode="w") as container:
        stream = container.add_stream("libx264", rate=args.fps)
        stream.width = width
        stream.height = height
        stream.pix_fmt = "yuv420p"
        stream.codec_context.time_base = time_base
        stream.options = {"crf": "20"}

        for index, name in enumerate(frames):
            path = os.path.join(args.frames_dir, name)
            with av.open(path) as image:
                for frame in image.decode(video=0):
                    # PNG frames carry no timestamp, so the frame rate has to
                    # be applied by hand.
                    frame.pts = index
                    frame.time_base = time_base
                    for packet in stream.encode(frame):
                        container.mux(packet)

        for packet in stream.encode():
            container.mux(packet)

    size_mb = os.path.getsize(args.output) / (1024 * 1024)
    print(f"wrote {args.output} ({len(frames)} frames, {size_mb:.1f} MB)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
