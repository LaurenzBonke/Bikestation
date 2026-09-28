<?php
declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(404);
    exit;
}

$email = $argv[1] ?? '';
if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
    fwrite(STDERR, "Usage: php create_admin.php admin@example.com\n");
    exit(1);
}

$host = getenv('DB_HOST') ?: '127.0.0.1';
$port = getenv('DB_PORT') ?: '3306';
$database = getenv('DB_NAME') ?: 'bikestation';
$username = getenv('DB_USER') ?: 'root';
$dbPassword = getenv('DB_PASSWORD') ?: '';

try {
    $pdo = new PDO(
        "mysql:host={$host};port={$port};dbname={$database};charset=utf8mb4",
        $username,
        $dbPassword,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
    fwrite(STDOUT, 'New admin password: ');
    $password = trim((string) fgets(STDIN));
    if (strlen($password) < 12) {
        fwrite(STDERR, "Use a password with at least 12 characters.\n");
        exit(1);
    }

    $insert = $pdo->prepare('INSERT INTO admins (email, password_hash) VALUES (?, ?)');
    $insert->execute([$email, password_hash($password, PASSWORD_DEFAULT)]);
    fwrite(STDOUT, "Admin account created for {$email}.\n");
} catch (Throwable $error) {
    fwrite(STDERR, "Could not create admin: {$error->getMessage()}\n");
    exit(1);
}