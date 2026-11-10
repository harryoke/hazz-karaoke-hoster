"""Local rolling HDemucs worker. Binary stdin/stdout; never accesses the network."""
import hashlib
import os
from pathlib import Path
import struct
import sys

os.environ.setdefault("OMP_NUM_THREADS", "2")
os.environ.setdefault("MKL_NUM_THREADS", "2")
import numpy as np
import torch
from torchaudio.models import hdemucs_high

RATE = 44100
HASH = "a004b2790d73ffeaa535db458a1a79b539dfdbafbccc31f275d07e632ebd7816"


def main():
    weights = Path(__file__).with_name("hdemucs_high_trained.pt")
    with weights.open("rb") as f:
        if hashlib.file_digest(f, "sha256").hexdigest() != HASH:
            raise ValueError("Stem model checksum mismatch")
    torch.set_num_threads(2)
    torch.set_num_interop_threads(1)
    model = hdemucs_high(sources=["drums", "bass", "other", "vocals"])
    model.load_state_dict(torch.load(weights, map_location="cpu", weights_only=True))
    model.eval()
    reader, writer = sys.stdin.buffer, sys.stdout.buffer
    writer.write(b"HST1")
    writer.flush()
    with torch.inference_mode():
        while header := reader.read(4):
            frames, = struct.unpack("<I", header)
            if not 1 <= frames <= 6 * RATE:
                raise ValueError("Invalid audio block length")
            data = reader.read(frames * 8)
            if len(data) != frames * 8:
                raise EOFError("Incomplete audio block")
            pcm = np.frombuffer(data, dtype="<f4").reshape(frames, 2)
            wave = torch.from_numpy(np.nan_to_num(pcm.T.copy(), nan=0, posinf=0, neginf=0))
            mean = wave.mean()
            std = wave.std(unbiased=False).clamp_min(1e-5)
            result = model(((wave - mean) / std).unsqueeze(0))[0] * std + mean / 4
            samples = result.permute(0, 2, 1).contiguous().cpu().numpy()
            writer.write(np.nan_to_num(samples, nan=0, posinf=0, neginf=0).astype("<f4", copy=False).tobytes())
            writer.flush()


if __name__ == "__main__":
    try:
        main()
    except (BrokenPipeError, EOFError):
        pass
    except Exception as ex:
        print(str(ex), file=sys.stderr)
        sys.exit(1)
