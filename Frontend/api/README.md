# Pedal bike station

## Local database setup

1. Start MySQL from the XAMPP Control Panel.
2. Open phpMyAdmin, choose **Import**, select `database.sql`, and run it. The script creates and seeds the `bikestation` database.
3. The local defaults are MySQL host `127.0.0.1`, port `3306`, user `root`, and an empty password. Set `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, or `DB_PASSWORD` in the PHP process environment if your local MySQL credentials differ.
4. From this folder, start the PHP API server:

   ```powershell
   & 'C:\xampp\php\php.exe' -S 127.0.0.1:8000
   ```

5. In a second terminal, start the frontend:

   ```powershell
   npm run dev
   ```

The Vite development server proxies `/api.php` to PHP. Keep both servers running while using the app.

## Create the first admin

Run this once in a terminal from this folder, replacing the email with the admin's address:

```powershell
& 'C:\xampp\php\php.exe' .\create_admin.php admin@example.com
```

Enter a password of at least 12 characters when prompted. Only its password hash is stored. The admin page is available from the **Admin** link in the top navigation. Admin sessions use HTTP-only cookies and a CSRF token for changes.

## Data model

- `stations` stores public station details and opening hours.
- `parking_spots` stores each spot's availability; the public free count is derived from these rows.
- `admins` stores administrator emails and password hashes.

The admin screen edits station details and each existing spot's status. To change a station's number of physical spots, update `parking_spots` in phpMyAdmin; the admin screen intentionally does not add or delete physical spots.