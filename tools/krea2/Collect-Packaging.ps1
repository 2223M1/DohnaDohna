$ErrorActionPreference = 'Stop'
$packagingRoot = 'D:\AI\ComfyUI-aki\training\dohnadohna-krea2-v1\revisions\official-gyokai-v2\official\packaging\fronts'
New-Item -ItemType Directory -Path $packagingRoot -Force | Out-Null
$packagingRoles = @(
    @{Role='alyce'; Product=1909}, @{Role='antena'; Product=1912},
    @{Role='kikuchiyo'; Product=1910}, @{Role='medhico'; Product=1911},
    @{Role='kirakira'; Product=1908}, @{Role='porno'; Product=1907}
)
$packagingRecords = @()
foreach ($entry in $packagingRoles) {
    $pageUrl = 'https://tamatoys.tma.co.jp/item/detail/TMT-' + $entry.Product
    $page = Invoke-WebRequest -Uri $pageUrl -TimeoutSec 25
    $pagePath = Join-Path $packagingRoot ($entry.Role + '-page.html')
    [IO.File]::WriteAllText($pagePath, $page.Content)
    $imageUrls = @([regex]::Matches($page.Content, 'https://prod-tamatoys\.s3\.amazonaws\.com/[^\s"<>]+\.jpg') |
        ForEach-Object { [Net.WebUtility]::HtmlDecode($_.Value) } | Select-Object -Unique)
    if ($imageUrls.Count -eq 0) { throw "No public product image: $pageUrl" }
    $targetPath = Join-Path $packagingRoot ($entry.Role + '-TMT-' + $entry.Product + '-front.jpg')
    Invoke-WebRequest -Uri $imageUrls[0] -OutFile $targetPath -TimeoutSec 25
    $packagingRecords += @{
        id=($entry.Role + '-tamatoys-front'); character=$entry.Role; source=$targetPath;
        source_name=$imageUrls[0]; source_sha256=(Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash.ToLower();
        kind='official'; group=('packaging/tamatoys/' + $entry.Role); page_url=$pageUrl;
        artist='魚介'; provenance='Manufacturer product front photograph; six new drawings credited to Gyokai by official AliceSoft announcements. Text and package perspective remain in source.';
        all_product_images=$imageUrls; decision='pending_crop_review'
    }
    $packagingRecords | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $packagingRoot 'manifest.json') -Encoding utf8
    Write-Output ($entry.Role + ': saved manufacturer front')
}
