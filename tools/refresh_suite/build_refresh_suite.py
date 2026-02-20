import argparse
import datetime
import shutil
from pathlib import Path


FIELDS = [
    ("Data", "D", 8, 0),
    ("Ore", "N", 2, 0),
    ("Minuti", "N", 2, 0),
    ("Secondi", "N", 2, 0),
    ("mSecondi", "N", 3, 0),
    ("A-01", "N", 9, 2),
    ("C-01", "N", 9, 2),
]


def _format_number(value, length, decimals):
    if value is None:
        return (" " * length).encode("ascii")
    if decimals > 0:
        text = f"{float(value):.{decimals}f}"
    else:
        text = str(int(value))
    if len(text) > length:
        raise ValueError(f"Numeric value '{text}' does not fit field length {length}")
    return text.rjust(length, " ").encode("ascii")


def _format_field(field, value):
    _, field_type, length, decimals = field
    if field_type == "D":
        if not isinstance(value, datetime.datetime):
            raise ValueError("Date field expects datetime value")
        return value.strftime("%Y%m%d").encode("ascii")
    if field_type == "N":
        return _format_number(value, length, decimals)
    raise ValueError(f"Unsupported field type: {field_type}")


def write_dbf(path, fields, rows):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)

    record_count = len(rows)
    header_len = 32 + 32 * len(fields) + 1
    record_len = 1 + sum(f[2] for f in fields)

    today = datetime.date.today()
    header = bytearray(32)
    header[0] = 0x03
    header[1] = today.year - 1900
    header[2] = today.month
    header[3] = today.day
    header[4:8] = int(record_count).to_bytes(4, "little", signed=False)
    header[8:10] = int(header_len).to_bytes(2, "little", signed=False)
    header[10:12] = int(record_len).to_bytes(2, "little", signed=False)

    descriptors = bytearray()
    for name, field_type, length, decimals in fields:
        desc = bytearray(32)
        encoded_name = name.encode("ascii")
        if len(encoded_name) > 11:
            raise ValueError(f"Field name '{name}' is longer than 11 bytes")
        desc[0:len(encoded_name)] = encoded_name
        desc[11] = ord(field_type)
        desc[16] = length
        desc[17] = decimals
        descriptors.extend(desc)

    terminator = b"\x0D"

    records = bytearray()
    for row in rows:
        rec = bytearray(record_len)
        rec[0] = 0x20
        offset = 1
        for field in fields:
            name = field[0]
            rec[offset:offset + field[2]] = _format_field(field, row.get(name))
            offset += field[2]
        records.extend(rec)

    eof = b"\x1A"
    with path.open("wb") as f:
        f.write(header)
        f.write(descriptors)
        f.write(terminator)
        f.write(records)
        f.write(eof)


def build_rows(base_dt, values_a, values_c):
    rows = []
    for i, (va, vc) in enumerate(zip(values_a, values_c)):
        dt = base_dt + datetime.timedelta(seconds=i)
        rows.append(
            {
                "Data": dt,
                "Ore": dt.hour,
                "Minuti": dt.minute,
                "Secondi": dt.second,
                "mSecondi": 0,
                "A-01": va,
                "C-01": vc,
            }
        )
    return rows


def write_canali_def(path):
    content = "\n".join(
        [
            "2",
            "A-01;Temperature A;C",
            "C-01;Pressure C;bar",
            "",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def write_prova_dat(path, source_name):
    content = "\n".join(
        [
            f"Source;{source_name}",
            "Operator;refresh-suite",
            "",
        ]
    )
    path.write_text(content, encoding="utf-8")


def copy_active_variant(source_dir, variant):
    src = source_dir / f"Prova001_{variant}.dbf"
    dst = source_dir / "Prova001.dbf"
    shutil.copyfile(str(src), str(dst))


def create_suite(root):
    root.mkdir(parents=True, exist_ok=True)

    source_a = root / "source_a"
    source_b = root / "source_b"

    for source_dir, source_name in [(source_a, "source_a"), (source_b, "source_b")]:
        (source_dir / "Set").mkdir(parents=True, exist_ok=True)
        write_canali_def(source_dir / "Set" / "Canali.def")
        write_prova_dat(source_dir / "Prova001.dat", source_name)

    base_a = datetime.datetime(2026, 2, 20, 10, 0, 0)
    base_b = datetime.datetime(2026, 2, 20, 10, 5, 0)

    rows_a_v1 = build_rows(base_a, [10, 11, 12, 13, 14], [1.00, 1.05, 1.10, 1.15, 1.20])
    rows_a_v2 = build_rows(base_a, [30, 31, 32, 33, 34], [1.50, 1.55, 1.60, 1.65, 1.70])

    rows_b_v1 = build_rows(base_b, [20, 21, 22, 23, 24], [2.00, 2.05, 2.10, 2.15, 2.20])
    rows_b_v2 = build_rows(base_b, [40, 41, 42, 43, 44], [2.50, 2.55, 2.60, 2.65, 2.70])

    write_dbf(source_a / "Prova001_v1.dbf", FIELDS, rows_a_v1)
    write_dbf(source_a / "Prova001_v2.dbf", FIELDS, rows_a_v2)

    write_dbf(source_b / "Prova001_v1.dbf", FIELDS, rows_b_v1)
    write_dbf(source_b / "Prova001_v2.dbf", FIELDS, rows_b_v2)

    copy_active_variant(source_a, "v1")
    copy_active_variant(source_b, "v1")

    readme = root / "README.md"
    readme.write_text(
        "\n".join(
            [
                "# Refresh Feature Test Data",
                "",
                "This folder contains deterministic data for manual testing of the Refresh button.",
                "",
                "- `source_a/Prova001.dbf` and `source_b/Prova001.dbf` are active files loaded by the app.",
                "- `*_v1.dbf` and `*_v2.dbf` are variants used to simulate external data updates.",
                "",
                "Switch active variant:",
                "",
                "```bash",
                "python tools/refresh_suite/switch_refresh_variant.py --variant v2",
                "python tools/refresh_suite/switch_refresh_variant.py --variant v1",
                "```",
                "",
            ]
        ),
        encoding="utf-8",
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        default=str(Path(__file__).resolve().parents[2] / "testdata" / "refresh_suite"),
        help="Target folder for generated refresh-suite data",
    )
    args = parser.parse_args()
    create_suite(Path(args.root))
    print(f"Refresh suite generated in: {args.root}")


if __name__ == "__main__":
    main()
