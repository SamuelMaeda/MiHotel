CREATE TABLE IF NOT EXISTS reserva_facturacion (
    id_reserva INT NOT NULL,
    requiere_factura TINYINT(1) NULL,
    estado_facturacion ENUM('sin_definir','no_solicitada','pendiente','registrada','anulada') NOT NULL DEFAULT 'sin_definir',
    estado_administrativo ENUM('pendiente_revision','cerrado') NOT NULL DEFAULT 'pendiente_revision',
    fecha_decision DATETIME NULL,
    id_usuario_decision INT NULL,
    fecha_actualizacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    id_usuario_actualizacion INT NULL,
    PRIMARY KEY (id_reserva),
    KEY ix_reserva_facturacion_estado (estado_facturacion),
    KEY ix_reserva_facturacion_fecha (fecha_decision),
    CONSTRAINT fk_reserva_facturacion_reserva
        FOREIGN KEY (id_reserva) REFERENCES reserva(id_reserva),
    CONSTRAINT fk_reserva_facturacion_usuario_decision
        FOREIGN KEY (id_usuario_decision) REFERENCES usuario(id_usuario),
    CONSTRAINT fk_reserva_facturacion_usuario_actualizacion
        FOREIGN KEY (id_usuario_actualizacion) REFERENCES usuario(id_usuario)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS reserva_facturacion_historial (
    id_historial BIGINT NOT NULL AUTO_INCREMENT,
    id_reserva INT NOT NULL,
    accion VARCHAR(50) NOT NULL,
    requiere_factura_anterior TINYINT(1) NULL,
    requiere_factura_nuevo TINYINT(1) NULL,
    estado_anterior VARCHAR(30) NULL,
    estado_nuevo VARCHAR(30) NOT NULL,
    detalle VARCHAR(255) NULL,
    id_usuario INT NOT NULL,
    fecha_hora DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id_historial),
    KEY ix_facturacion_historial_reserva (id_reserva, fecha_hora),
    KEY ix_facturacion_historial_usuario (id_usuario),
    CONSTRAINT fk_facturacion_historial_reserva
        FOREIGN KEY (id_reserva) REFERENCES reserva(id_reserva),
    CONSTRAINT fk_facturacion_historial_usuario
        FOREIGN KEY (id_usuario) REFERENCES usuario(id_usuario)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS documento_fiscal (
    id_documento_fiscal BIGINT NOT NULL AUTO_INCREMENT,
    tipo_documento ENUM('factura','nota_credito','nota_debito') NOT NULL DEFAULT 'factura',
    nit_receptor VARCHAR(40) NULL,
    serie VARCHAR(50) NULL,
    numero_dte VARCHAR(50) NULL,
    contenido LONGBLOB NOT NULL,
    tipo_mime VARCHAR(100) NOT NULL DEFAULT 'application/pdf',
    nombre_original VARCHAR(255) NOT NULL,
    tamano BIGINT UNSIGNED NOT NULL,
    estado ENUM('vigente','anulado','sustituido') NOT NULL DEFAULT 'vigente',
    id_documento_origen BIGINT NULL,
    id_reserva_factura_legacy INT NULL,
    id_usuario_registro INT NOT NULL,
    fecha_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    id_usuario_estado INT NULL,
    fecha_estado DATETIME NULL,
    motivo_estado VARCHAR(255) NULL,
    PRIMARY KEY (id_documento_fiscal),
    UNIQUE KEY uq_documento_serie_numero (serie, numero_dte),
    UNIQUE KEY uq_documento_legacy (id_reserva_factura_legacy),
    KEY ix_documento_nit (nit_receptor),
    KEY ix_documento_estado (estado),
    KEY ix_documento_usuario (id_usuario_registro),
    CONSTRAINT fk_documento_usuario_registro
        FOREIGN KEY (id_usuario_registro) REFERENCES usuario(id_usuario),
    CONSTRAINT fk_documento_usuario_estado
        FOREIGN KEY (id_usuario_estado) REFERENCES usuario(id_usuario),
    CONSTRAINT fk_documento_origen
        FOREIGN KEY (id_documento_origen) REFERENCES documento_fiscal(id_documento_fiscal)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS documento_fiscal_reserva (
    id_documento_fiscal BIGINT NOT NULL,
    id_reserva INT NOT NULL,
    PRIMARY KEY (id_documento_fiscal, id_reserva),
    KEY ix_documento_reserva_reserva (id_reserva),
    CONSTRAINT fk_documento_reserva_documento
        FOREIGN KEY (id_documento_fiscal) REFERENCES documento_fiscal(id_documento_fiscal),
    CONSTRAINT fk_documento_reserva_reserva
        FOREIGN KEY (id_reserva) REFERENCES reserva(id_reserva)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO documento_fiscal
    (tipo_documento, nit_receptor, serie, numero_dte, contenido, tipo_mime,
     nombre_original, tamano, estado, id_reserva_factura_legacy,
     id_usuario_registro, fecha_registro)
SELECT
    'factura', NULL, NULL, NULL, rf.contenido, rf.tipo_mime,
    rf.nombre_original, rf.tamano, 'vigente', rf.id_reserva_factura,
    rf.id_usuario, rf.fecha_subida
FROM reserva_factura rf
ON DUPLICATE KEY UPDATE id_reserva_factura_legacy = VALUES(id_reserva_factura_legacy);

INSERT IGNORE INTO documento_fiscal_reserva (id_documento_fiscal, id_reserva)
SELECT df.id_documento_fiscal, rf.id_reserva
FROM documento_fiscal df
INNER JOIN reserva_factura rf
    ON rf.id_reserva_factura = df.id_reserva_factura_legacy
WHERE rf.id_reserva IS NOT NULL;

INSERT IGNORE INTO documento_fiscal_reserva (id_documento_fiscal, id_reserva)
SELECT df.id_documento_fiscal, r.id_reserva
FROM documento_fiscal df
INNER JOIN reserva_factura rf
    ON rf.id_reserva_factura = df.id_reserva_factura_legacy
INNER JOIN reserva r
    ON r.id_reserva_grupo = rf.id_reserva_grupo
WHERE rf.id_reserva_grupo IS NOT NULL;

INSERT INTO reserva_facturacion
    (id_reserva, requiere_factura, estado_facturacion, estado_administrativo,
     fecha_decision, id_usuario_decision, id_usuario_actualizacion)
SELECT DISTINCT
    dfr.id_reserva, 1, 'registrada', 'cerrado',
    df.fecha_registro, df.id_usuario_registro, df.id_usuario_registro
FROM documento_fiscal_reserva dfr
INNER JOIN documento_fiscal df
    ON df.id_documento_fiscal = dfr.id_documento_fiscal
ON DUPLICATE KEY UPDATE
    requiere_factura = 1,
    estado_facturacion = 'registrada',
    estado_administrativo = 'cerrado',
    id_usuario_actualizacion = VALUES(id_usuario_actualizacion);
