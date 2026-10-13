import sys
from pypdf import PdfReader

sys.stdout.reconfigure(encoding="utf-8")
documento = PdfReader(sys.argv[1], strict=True)
for pagina in documento.pages:
    print(pagina.extract_text(extraction_mode="layout") or "", end="\f")
