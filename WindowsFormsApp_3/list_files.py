import os

base = r"C:\Users\pc\Desktop\最近报告\win\3sheji\WindowsFormsApp_3"
for root, dirs, files in os.walk(base):
    dirs[:] = [d for d in dirs if d not in ('obj','bin','.vs','Properties','.claude','libs','test_evidence')]
    for f in sorted(files):
        if f.endswith('.cs'):
            full = os.path.join(root, f)
            print(full)
    if root != base:
        dirs.clear()
