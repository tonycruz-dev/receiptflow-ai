# ReceiptFlow AI

## NVIDIA Receipt Extraction

ReceiptFlow uses the `IDocumentExtractor` abstraction for receipt OCR and
field extraction. The current implementation calls a configured
NVIDIA-hosted NIM endpoint with an OpenAI-compatible chat completions API.

Configure the worker with an OCR-capable multimodal NVIDIA model:

```json
"Nvidia": {
  "Endpoint": "https://integrate.api.nvidia.com/v1",
  "Model": "your-ocr-capable-vision-model",
  "MaxPdfPages": 5,
  "MinimumConfidence": 0.70
}
```

Set the API key outside tracked files:

```powershell
dotnet user-secrets set "Nvidia:ApiKey" "<key>" --project .\ReceiptFlow.DocumentWorker
# or
$env:NVIDIA_API_KEY = "<key>"
```

Hosted inference sends receipt image/PDF content to the configured NVIDIA
provider. Automated tests use mocked HTTP handlers and do not call NVIDIA or
consume API quota.
