<#
.SYNOPSIS
Provisions the test_user account in the isolated MySQL test container with minimal required privileges.

.DESCRIPTION
This script grants the necessary privileges to the pre-existing 'test_user' account so that
MySqlAisladoFixture and DatabaseInitializer can create, initialize, and drop temporary
databases following the pattern 'autotaller_test_<GUID>'.

The script connects exclusively to the isolated test container 'tallermecanico-mysql-test'
on port 3308, verifies container health, and uses the root password from the container's
environment variables without ever exposing credentials to the host.

.REQUIREMENTS
- Windows PowerShell 5.1+
- Docker with the test container running and healthy
- .env.test file with MYSQL_TEST_ROOT_PASSWORD (not loaded automatically)

.NOTES
- Idempotent: safe to run multiple times.
- No passwords in SQL, command line, temp files, or logs.
- Root password never leaves the container; MYSQL_PWD expanded inside container via shell.
- Uses docker exec with PowerShell pipeline, no Start-Process, no Invoke-Expression.
#>

param()

#region Configuration
$containerName = 'tallermecanico-mysql-test'
$testUser = 'test_user'
# MySQL pattern: backticks for identifier, backslash escapes underscore for literal match
$dbPattern = 'autotaller\_test\_%'
$expectedHostBinding = '127.0.0.1:3308'
#endregion

#region Helper Functions
function Write-ErrorAndExit {
    param([string]$Message, [int]$ExitCode = 1)
    Write-Error $Message
    exit $ExitCode
}

function Test-ContainerExists {
    param([string]$Name)
    $container = docker ps -a --filter "name=^/$Name$" --format '{{.Names}}'
    return $container -eq $Name
}

function Test-ContainerRunning {
    param([string]$Name)
    $state = docker inspect --format='{{.State.Status}}' $Name 2>$null
    return $state -eq 'running'
}

function Test-ContainerHealthy {
    param([string]$Name)
    $health = docker inspect --format='{{.State.Health.Status}}' $Name 2>$null
    return $health -eq 'healthy'
}

function Test-ContainerPortBinding {
    param([string]$Name, [string]$ExpectedBinding)
    # docker port output format: "3306/tcp -> 127.0.0.1:3308"
    # Must match exactly the expected binding; reject 0.0.0.0
    $ports = docker port $Name 2>$null
    if (-not $ports) { return $false }
    return $ports -match [regex]::Escape($ExpectedBinding) -and $ports -notmatch '0\.0\.0\.0'
}

function Invoke-MysqlInContainer {
    param(
        [string]$ContainerName,
        [string]$Sql,
        [string]$User = 'root'
    )
    # Execute SQL via stdin inside container using PowerShell pipeline.
    # sh -c expands MYSQL_PWD from container's environment.
    # No password ever touches host process args, env, stdout, or files.
    # Use single-quoted PowerShell string so $MYSQL_ROOT_PASSWORD passes literally to container shell.
    $shellCmd = 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysql -u ' + $User + ' --silent --skip-column-names'

    try {
        $result = $Sql | & docker.exe exec -i $ContainerName sh -c $shellCmd
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            throw "MySQL command failed (exit $exitCode): $result"
        }
        return (($result | Out-String).Trim())
    }
    catch {
        throw "MySQL execution error: $($_.Exception.Message)"
    }
}
#endregion

#region Main Logic
try {
    Write-Host "=== MySQL Test User Provisioning ===" -ForegroundColor Cyan
    Write-Host "Target container: $containerName"
    Write-Host "Test user: $testUser"
    Write-Host "Database pattern: $dbPattern"
    Write-Host ""

    # 1. Verify container exists
    Write-Host "Verifying container existence..."
    if (-not (Test-ContainerExists $containerName)) {
        Write-ErrorAndExit "Container '$containerName' does not exist. Start it with: docker compose --env-file .env.test -f docker-compose.test.yml up -d"
    }
    Write-Host "  Container exists." -ForegroundColor Green

    # 2. Verify container is running
    Write-Host "Verifying container state..."
    if (-not (Test-ContainerRunning $containerName)) {
        Write-ErrorAndExit "Container '$containerName' is not running."
    }
    Write-Host "  Container is running." -ForegroundColor Green

    # 3. Verify container is healthy
    Write-Host "Verifying container health..."
    if (-not (Test-ContainerHealthy $containerName)) {
        Write-ErrorAndExit "Container '$containerName' is not healthy. Wait for healthcheck or check logs: docker logs $containerName"
    }
    Write-Host "  Container is healthy." -ForegroundColor Green

    # 4. Verify published port binding is exactly 127.0.0.1:3308 (reject 0.0.0.0)
    Write-Host "Verifying published port binding $expectedHostBinding..."
    if (-not (Test-ContainerPortBinding $containerName $expectedHostBinding)) {
        Write-ErrorAndExit "Container '$containerName' does not publish $expectedHostBinding. This is not the isolated test container."
    }
    Write-Host "  Port binding $expectedHostBinding confirmed." -ForegroundColor Green

    # 5. Prepare SQL: minimum privileges for test databases only
    # MySqlAisladoFixture needs: CREATE DATABASE, DROP DATABASE on autotaller_test_%
    # DatabaseInitializer needs: CREATE, ALTER, DROP TABLE, CREATE/DROP INDEX, CREATE TRIGGER,
    # REFERENCES (FK), SELECT, INSERT, UPDATE, DELETE on tables within those databases
    # All restricted to `autotaller_test_%` pattern (literal underscores via backslash)
    # Use doubled backticks in here-string for literal MySQL backticks
    $grantSql = @"
GRANT CREATE, DROP, ALTER, INDEX, REFERENCES, TRIGGER, SELECT, INSERT, UPDATE, DELETE
ON ``$dbPattern``.* TO '${testUser}'@'%';
FLUSH PRIVILEGES;
"@

    Write-Host "Provisioning privileges for '$testUser' on databases matching '$dbPattern'..."

    # 6. Execute grant inside container - password never leaves container
    try {
        $null = Invoke-MysqlInContainer -ContainerName $containerName -Sql $grantSql
        Write-Host "  Privileges granted successfully." -ForegroundColor Green
    }
    catch {
        Write-ErrorAndExit "Provisioning failed: $($_.Exception.Message)"
    }

    # 7. Verify grants
    Write-Host "Verifying granted privileges..."
    $verifySql = "SHOW GRANTS FOR '$testUser'@'%';"
    try {
        $verifyResult = Invoke-MysqlInContainer -ContainerName $containerName -Sql $verifySql
        Write-Host "  Current grants for ${testUser}:"
        $verifyResult -split "`n" | ForEach-Object { Write-Host "    $_" }

        # 8. Confirm pattern-specific grant exists (check for escaped pattern in output)
        if ($verifyResult -notmatch [regex]::Escape($dbPattern)) {
            Write-Warning "Expected grant on pattern '$dbPattern' not clearly visible in output. Manual verification recommended."
        } else {
            Write-Host "  Pattern-specific grant confirmed." -ForegroundColor Green
        }
    }
    catch {
        Write-ErrorAndExit "Grant verification failed: $($_.Exception.Message)"
    }

    Write-Host ""
    Write-Host "=== Provisioning completed successfully ===" -ForegroundColor Cyan
    Write-Host "The user '$testUser' can now create, initialize, and drop databases matching 'autotaller_test_<GUID>'."
}
catch {
    Write-ErrorAndExit "Unexpected error: $($_.Exception.Message)"
}
#endregion