import argparse
import shutil
from pathlib import Path


def switch_variant(root, variant):
    for source in ["source_a", "source_b"]:
        source_dir = root / source
        src = source_dir / f"Prova001_{variant}.dbf"
        dst = source_dir / "Prova001.dbf"
        if not src.exists():
            raise FileNotFoundError(f"Variant file not found: {src}")
        shutil.copyfile(str(src), str(dst))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        default=str(Path(__file__).resolve().parents[2] / "testdata" / "refresh_suite"),
        help="Root folder with source_a/source_b",
    )
    parser.add_argument("--variant", choices=["v1", "v2"], required=True)
    args = parser.parse_args()

    root = Path(args.root)
    switch_variant(root, args.variant)
    print(f"Active variant set to {args.variant} in {root}")


if __name__ == "__main__":
    main()
