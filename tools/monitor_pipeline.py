"""Record peak process memory for one explicitly identified twin pipeline run."""
import argparse
import json
from pathlib import Path
import time
import psutil

p = argparse.ArgumentParser()
p.add_argument("pid", type=int)
p.add_argument("output", type=Path)
a = p.parse_args()
process = psutil.Process(a.pid)
assert any("twin_pipeline.py" in v for v in process.cmdline()), "Refuse to monitor an unrelated process"
peak = 0
while process.is_running():
    try:
        memory = process.memory_info()
        peak = max(peak, getattr(memory, "peak_wset", memory.rss))
        a.output.write_text(json.dumps({"pid": a.pid, "peak_working_set_bytes": peak, "limit_bytes": 8_000_000_000, "checked_unix": time.time()}, indent=2))
        if peak > 8_000_000_000:
            # Only the explicitly checked preprocessing process; checkpoints survive.
            process.terminate()
            raise MemoryError("Pipeline exceeded the 8 GB process-memory limit")
        time.sleep(10)
    except psutil.NoSuchProcess:
        break
print(f"Peak working set: {peak:,} bytes")
