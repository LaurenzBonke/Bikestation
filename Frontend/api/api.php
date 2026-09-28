<?php
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

if (session_status() !== PHP_SESSION_ACTIVE) {
    session_set_cookie_params([
        'httponly' => true,
        'secure' => !empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off',
        'samesite' => 'Strict',
        'path' => '/',
    ]);
    session_start();
}

function respond(int $status, array $body): never
{
    http_response_code($status);
    echo json_encode($body, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

function request_data(): array
{
    $data = json_decode(file_get_contents('php://input'), true);
    return is_array($data) ? $data : [];
}

function require_admin(): void
{
    if (!isset($_SESSION['admin_id'])) {
        respond(401, ['error' => 'Please sign in as an administrator.']);
    }

    $csrf = $_SERVER['HTTP_X_CSRF_TOKEN'] ?? '';
    if (!is_string($csrf) || !hash_equals($_SESSION['csrf_token'] ?? '', $csrf)) {
        respond(403, ['error' => 'Your session token is invalid. Refresh and try again.']);
    }
}

function station_data(PDO $pdo, int $id): ?array
{
    $stationQuery = $pdo->prepare(
        'SELECT id, name, address, neighborhood, description, opening_hours FROM stations WHERE id = ?'
    );
    $stationQuery->execute([$id]);
    $station = $stationQuery->fetch();

    if (!$station) {
        return null;
    }

    $spotsQuery = $pdo->prepare(
        'SELECT spot_number, is_available FROM parking_spots WHERE station_id = ? ORDER BY spot_number'
    );
    $spotsQuery->execute([$id]);
    $station['spots'] = array_map(
        static fn(array $spot): bool => (bool) $spot['is_available'],
        $spotsQuery->fetchAll()
    );
    $station['id'] = (int) $station['id'];
    return $station;
}

try {
    $host = getenv('DB_HOST') ?: '127.0.0.1';
    $port = getenv('DB_PORT') ?: '3306';
    $database = getenv('DB_NAME') ?: 'bikestation';
    $username = getenv('DB_USER') ?: 'root';
    $password = getenv('DB_PASSWORD') ?: '';
    $pdo = new PDO(
        "mysql:host={$host};port={$port};dbname={$database};charset=utf8mb4",
        $username,
        $password,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC]
    );

    $method = $_SERVER['REQUEST_METHOD'] ?? 'GET';
    $action = $_GET['action'] ?? 'stations';

    if ($method === 'GET' && $action === 'stations') {
        $ids = $pdo->query('SELECT id FROM stations ORDER BY id')->fetchAll(PDO::FETCH_COLUMN);
        $stations = array_values(array_filter(array_map(
            static fn(string|int $id): ?array => station_data($pdo, (int) $id),
            $ids
        )));
        respond(200, ['stations' => $stations]);
    }

    if ($method === 'GET' && $action === 'session') {
        if (!isset($_SESSION['admin_id'])) {
            respond(200, ['authenticated' => false]);
        }
        $_SESSION['csrf_token'] ??= bin2hex(random_bytes(32));
        respond(200, [
            'authenticated' => true,
            'email' => $_SESSION['admin_email'],
            'csrfToken' => $_SESSION['csrf_token'],
        ]);
    }

    if ($method === 'POST' && $action === 'login') {
        $body = request_data();
        $email = filter_var($body['email'] ?? '', FILTER_VALIDATE_EMAIL);
        $password = $body['password'] ?? '';
        if (!$email || !is_string($password) || $password === '') {
            respond(422, ['error' => 'Enter a valid email address and password.']);
        }

        $adminQuery = $pdo->prepare('SELECT id, email, password_hash FROM admins WHERE email = ?');
        $adminQuery->execute([$email]);
        $admin = $adminQuery->fetch();
        if (!$admin || !password_verify($password, $admin['password_hash'])) {
            respond(401, ['error' => 'Email or password is incorrect.']);
        }

        session_regenerate_id(true);
        $_SESSION['admin_id'] = (int) $admin['id'];
        $_SESSION['admin_email'] = $admin['email'];
        $_SESSION['csrf_token'] = bin2hex(random_bytes(32));
        respond(200, [
            'authenticated' => true,
            'email' => $admin['email'],
            'csrfToken' => $_SESSION['csrf_token'],
        ]);
    }

    if ($method === 'POST' && $action === 'logout') {
        require_admin();
        $_SESSION = [];
        session_destroy();
        respond(200, ['authenticated' => false]);
    }

    if ($method === 'PUT' && $action === 'station') {
        require_admin();
        $body = request_data();
        $id = filter_var($body['id'] ?? null, FILTER_VALIDATE_INT);
        $name = trim((string) ($body['name'] ?? ''));
        $address = trim((string) ($body['address'] ?? ''));
        $neighborhood = trim((string) ($body['neighborhood'] ?? ''));
        $description = trim((string) ($body['description'] ?? ''));
        $openingHours = trim((string) ($body['openingHours'] ?? ''));
        $spots = $body['spots'] ?? null;

        if (!$id || $name === '' || $address === '' || !is_array($spots) || count($spots) < 1 || count($spots) > 100) {
            respond(422, ['error' => 'Provide a station name, address, and valid spot statuses.']);
        }
        foreach ($spots as $status) {
            if (!is_bool($status)) {
                respond(422, ['error' => 'Each parking spot status must be available or occupied.']);
            }
        }

        $pdo->beginTransaction();
        $stationUpdate = $pdo->prepare(
            'UPDATE stations SET name = ?, address = ?, neighborhood = ?, description = ?, opening_hours = ? WHERE id = ?'
        );
        $stationUpdate->execute([$name, $address, $neighborhood, $description, $openingHours, $id]);
        if ($stationUpdate->rowCount() === 0 && !station_data($pdo, $id)) {
            $pdo->rollBack();
            respond(404, ['error' => 'Station not found.']);
        }

        $existingQuery = $pdo->prepare('SELECT COUNT(*) FROM parking_spots WHERE station_id = ?');
        $existingQuery->execute([$id]);
        $existingCount = (int) $existingQuery->fetchColumn();
        if ($existingCount !== count($spots)) {
            $pdo->rollBack();
            respond(422, ['error' => 'The number of spots cannot be changed from this screen.']);
        }

        $spotUpdate = $pdo->prepare(
            'UPDATE parking_spots SET is_available = ? WHERE station_id = ? AND spot_number = ?'
        );
        foreach ($spots as $index => $available) {
            $spotUpdate->execute([$available ? 1 : 0, $id, $index + 1]);
        }
        $pdo->commit();
        respond(200, ['station' => station_data($pdo, $id)]);
    }

    respond(404, ['error' => 'API action not found.']);
} catch (Throwable $error) {
    if (isset($pdo) && $pdo instanceof PDO && $pdo->inTransaction()) {
        $pdo->rollBack();
    }
    error_log((string) $error);
    respond(500, ['error' => 'The server could not complete the request. Check the database connection.']);
}