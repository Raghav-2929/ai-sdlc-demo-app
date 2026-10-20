"""Prints a per-file summary of a Stryker.NET mutation-report.json, plus the surviving mutants.

Usage:
    python scripts/mutation/summarize.py <mutation-report.json> [file-filter]

Score = (killed + timeout) / (killed + timeout + survived + no coverage), the same formula Stryker uses.
Pass a file filter (for example ApprovalService) to list that file's surviving and uncovered mutants.
"""

import collections
import json
import sys


def score(counts):
    detected = counts["Killed"] + counts["Timeout"]
    valid = detected + counts["Survived"] + counts["NoCoverage"]
    return f"{100 * detected / valid:.2f}%" if valid else "n/a"


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    with open(sys.argv[1], encoding="utf-8") as report_file:
        report = json.load(report_file)
    file_filter = sys.argv[2] if len(sys.argv) > 2 else None

    columns = ("Killed", "Timeout", "Survived", "NoCoverage", "Ignored", "CompileError")
    print(f"{'File':<30}{'Score':>8}" + "".join(f"{name:>13}" for name in columns))
    total = collections.Counter()
    for path, file_report in sorted(report["files"].items()):
        counts = collections.Counter(mutant["status"] for mutant in file_report["mutants"])
        total.update(counts)
        name = path.replace("\\", "/").split("/")[-1]
        print(f"{name:<30}{score(counts):>8}" + "".join(f"{counts[column]:>13}" for column in columns))
    print(f"{'TOTAL':<30}{score(total):>8}" + "".join(f"{total[column]:>13}" for column in columns))

    if file_filter:
        print(f"\nSurviving / uncovered mutants in files matching '{file_filter}':")
        for path, file_report in sorted(report["files"].items()):
            if file_filter not in path:
                continue
            for mutant in file_report["mutants"]:
                if mutant["status"] in ("Survived", "NoCoverage"):
                    line = mutant["location"]["start"]["line"]
                    print(f"  {mutant['status']:<11} line {line:>3}  {mutant['mutatorName']}: {mutant.get('replacement', '')[:70]}")


if __name__ == "__main__":
    main()
