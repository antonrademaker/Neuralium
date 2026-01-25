# Add arXiv sources to the Neuralium database
# This script uses Npgsql to connect to the database and insert records

Write-Host "Adding arXiv sources to Neuralium database..." -ForegroundColor Cyan

# Get connection string from environment or use default
$connectionString = $env:NEURALIUM_CONNECTIONSTRING
if (-not $connectionString) {
    $connectionString = "Host=localhost;Port=61853;Database=neuralium;Username=postgres;Password=DevelopmentPassword123!"
    Write-Host "Using default connection string (port 61853)" -ForegroundColor Yellow
    Write-Host "If connection fails, check Aspire dashboard for current postgres port" -ForegroundColor Yellow
}

# SQL to insert arXiv sources
$sql = @"
INSERT INTO feed_sources ("Name", "Type", "Url", "Enabled", "TimeoutSeconds", "MinimumFetchIntervalMinutes", "CreatedAtUtc")
VALUES 
    ('arXiv AI Research', 'Arxiv', 'cat:cs.AI OR cat:cs.LG OR cat:cs.CL', true, 45, 240, NOW()),
    ('arXiv Neural Networks', 'Arxiv', 'all:neural network OR all:deep learning', true, 45, 240, NOW()),
    ('arXiv Large Language Models', 'Arxiv', 'all:large language model OR all:LLM OR all:transformer', true, 45, 240, NOW())
ON CONFLICT DO NOTHING;
"@

try {
    # Load Npgsql assembly (requires Npgsql NuGet package)
    Add-Type -Path "$PSScriptRoot\..\src\Neuralium.Data\bin\Debug\net10.0\Npgsql.dll" -ErrorAction Stop
    
    $conn = New-Object Npgsql.NpgsqlConnection($connectionString)
    $conn.Open()
    
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $rowsAffected = $cmd.ExecuteNonQuery()
    
    Write-Host "✓ Successfully added $rowsAffected arXiv source(s)" -ForegroundColor Green
    
    # Query to show all arXiv sources
    $cmd.CommandText = "SELECT ""Id"", ""Name"", ""Type"", ""Url"", ""Enabled"" FROM feed_sources WHERE ""Type"" = 'Arxiv' ORDER BY ""Id"";"
    $reader = $cmd.ExecuteReader()
    
    Write-Host "`narXiv sources in database:" -ForegroundColor Cyan
    while ($reader.Read()) {
        Write-Host "  [$($reader['Id'])] $($reader['Name']): $($reader['Url'])" -ForegroundColor White
    }
    
    $reader.Close()
    $conn.Close()
    
    Write-Host "`nRun the worker to test arXiv integration:" -ForegroundColor Yellow
    Write-Host "  dotnet run --project src/Neuralium.Worker/Neuralium.Worker.csproj" -ForegroundColor Gray
}
catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nAlternative: Use pgAdmin web interface at http://localhost:61852" -ForegroundColor Yellow
    Write-Host "Login: admin@neuralium.local / DevelopmentPassword123!" -ForegroundColor Gray
    Write-Host "Then run the SQL from: docs/add-arxiv-sources.sql" -ForegroundColor Gray
    exit 1
}
