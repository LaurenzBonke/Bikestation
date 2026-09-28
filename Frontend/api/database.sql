CREATE DATABASE IF NOT EXISTS bikestation
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE bikestation;

CREATE TABLE IF NOT EXISTS stations (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  name VARCHAR(120) NOT NULL,
  address VARCHAR(255) NOT NULL,
  neighborhood VARCHAR(120) NOT NULL DEFAULT '',
  description TEXT NOT NULL,
  opening_hours VARCHAR(120) NOT NULL DEFAULT 'Open 24 hours',
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS parking_spots (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  station_id INT UNSIGNED NOT NULL,
  spot_number SMALLINT UNSIGNED NOT NULL,
  is_available TINYINT(1) NOT NULL DEFAULT 1,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uq_station_spot (station_id, spot_number),
  CONSTRAINT fk_spots_station FOREIGN KEY (station_id)
    REFERENCES stations (id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS admins (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  email VARCHAR(190) NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uq_admin_email (email)
) ENGINE=InnoDB;

INSERT INTO stations (id, name, address, neighborhood, description, opening_hours)
VALUES
  (1, 'Central Library', '128 Civic Plaza', 'Downtown', 'Secure bike parking beside the Central Library entrance.', 'Open 24 hours'),
  (2, 'Riverside Market', '42 Riverwalk Avenue', 'Riverside', 'Bike parking near the north entrance of Riverside Market.', 'Daily, 6:00 AM - 11:00 PM'),
  (3, 'North Campus', '9 University Way', 'University District', 'Covered bike parking at the North Campus transit stop.', 'Open 24 hours')
ON DUPLICATE KEY UPDATE id = VALUES(id);

INSERT INTO parking_spots (station_id, spot_number, is_available)
VALUES
  (1, 1, 1), (1, 2, 0), (1, 3, 1),
  (2, 1, 1), (2, 2, 1), (2, 3, 0),
  (3, 1, 0), (3, 2, 1), (3, 3, 0)
ON DUPLICATE KEY UPDATE station_id = VALUES(station_id);