# Minimal static web server for testing the Ashen Realm WebGL build locally.
# Usage (from the project folder):  powershell -ExecutionPolicy Bypass -File tools/serve-webgl.ps1
# Then open http://localhost:8080 in a browser. Ctrl+C stops the server.
param(
    [string]$Root = (Join-Path $PSScriptRoot "..\Builds\WebGL"),
    [int]$Port = 8080
)

$Root = (Resolve-Path $Root).Path
$types = @{
    ".html" = "text/html; charset=utf-8"; ".css" = "text/css"; ".js" = "application/javascript"
    ".wasm" = "application/wasm"; ".data" = "application/octet-stream"; ".json" = "application/json"
    ".png" = "image/png"; ".ico" = "image/x-icon"; ".symbols.json" = "application/json"
}

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()
Write-Host "Serving $Root at http://localhost:$Port/  (Ctrl+C to stop)"

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $path = [Uri]::UnescapeDataString($context.Request.Url.AbsolutePath.TrimStart('/'))
        if ([string]::IsNullOrEmpty($path)) { $path = "index.html" }
        $file = [System.IO.Path]::GetFullPath((Join-Path $Root $path))
        $response = $context.Response
        if (-not $file.StartsWith($Root) -or -not (Test-Path $file -PathType Leaf)) {
            $response.StatusCode = 404
            $response.Close()
            continue
        }
        $name = [System.IO.Path]::GetFileName($file)
        $inner = $name
        if ($name.EndsWith(".gz")) {
            $response.AddHeader("Content-Encoding", "gzip")
            $inner = $name.Substring(0, $name.Length - 3)
        }
        $ext = [System.IO.Path]::GetExtension($inner)
        $response.ContentType = if ($types.ContainsKey($ext)) { $types[$ext] } else { "application/octet-stream" }
        $response.AddHeader("Cache-Control", "no-cache")
        $bytes = [System.IO.File]::ReadAllBytes($file)
        $response.ContentLength64 = $bytes.Length
        $response.OutputStream.Write($bytes, 0, $bytes.Length)
        $response.Close()
    }
}
finally {
    $listener.Stop()
}
