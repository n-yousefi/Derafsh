from pathlib import Path

FILES = {
    "Derafsh": Path("comparison/Derafsh.SettingsComparison/DerafshSettingsStore.cs"),
    "Manual Dapper": Path("comparison/Derafsh.SettingsComparison/ManualDapperSettingsStore.cs"),
}

def code_lines(path: Path) -> int:
    count = 0
    in_block = False
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line:
            continue
        if in_block:
            if "*/" in line:
                in_block = False
            continue
        if line.startswith("/*"):
            if "*/" not in line:
                in_block = True
            continue
        if line.startswith("//"):
            continue
        count += 1
    return count

counts = {name: code_lines(path) for name, path in FILES.items()}
manual = counts["Manual Dapper"]
derafsh = counts["Derafsh"]
reduction = (manual - derafsh) / manual * 100

for name, count in counts.items():
    print(f"{name}: {count} non-blank, non-comment lines")
print(f"Reduction: {reduction:.1f}%")
