--
--
-- Esto aplica solo para cuando se quiere rehacer la base de datos
-- DROP DATABASE IF EXISTS Hotel;

-- CREATE DATABASE Hotel
-- CHARACTER SET utf8mb4
-- COLLATE utf8mb4_unicode_ci;
-- 
--
USE Hotel;

-- Evita que nombres con mayúsculas/minúsculas o caracteres especiales se
-- almacenen con una codificación distinta a la definida para el proyecto.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;

-- ============================================================================
-- 1. SEGURIDAD, ROLES Y PERMISOS
-- ============================================================================

CREATE TABLE rol (
    id_rol INT NOT NULL AUTO_INCREMENT,
    nombre_rol VARCHAR(50) NOT NULL,
    estado ENUM('activo', 'inactivo') NOT NULL DEFAULT 'activo',
    PRIMARY KEY (id_rol),
    CONSTRAINT uq_rol_nombre UNIQUE (nombre_rol)
) ENGINE = InnoDB;

CREATE TABLE permisos (
    id_permiso INT NOT NULL AUTO_INCREMENT,
    nombre_permiso VARCHAR(100) NOT NULL,
    descripcion VARCHAR(255) NULL,
    estado TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (id_permiso),
    CONSTRAINT uq_permisos_nombre UNIQUE (nombre_permiso)
) ENGINE = InnoDB;

CREATE TABLE rol_permiso (
    id_rol INT NOT NULL,
    id_permiso INT NOT NULL,
    PRIMARY KEY (id_rol, id_permiso),
    CONSTRAINT fk_rol_permiso_rol
        FOREIGN KEY (id_rol)
        REFERENCES rol (id_rol)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT fk_rol_permiso_permiso
        FOREIGN KEY (id_permiso)
        REFERENCES permisos (id_permiso)
        ON UPDATE CASCADE
        ON DELETE CASCADE
) ENGINE = InnoDB;

CREATE TABLE usuario (
    id_usuario INT NOT NULL AUTO_INCREMENT,
    id_rol INT NOT NULL,
    nombre_usuario VARCHAR(100) NOT NULL,
    correo VARCHAR(150) NOT NULL,
    telefono VARCHAR(20) NULL,
    clave VARCHAR(255) NOT NULL,
    fecha_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    estado ENUM('activo', 'inactivo') NOT NULL DEFAULT 'activo',
    token_recordarme VARCHAR(255) NULL,
    fecha_expiracion_recordarme DATETIME NULL,
    token_recuperacion VARCHAR(200) NULL,
    fecha_expiracion_recuperacion DATETIME NULL,
    PRIMARY KEY (id_usuario),
    CONSTRAINT uq_usuario_correo UNIQUE (correo),
    CONSTRAINT fk_usuario_rol
        FOREIGN KEY (id_rol)
        REFERENCES rol (id_rol)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE = InnoDB;

CREATE INDEX idx_usuario_id_rol ON usuario (id_rol);
CREATE INDEX idx_usuario_estado ON usuario (estado);
CREATE INDEX idx_usuario_token_recordarme ON usuario (token_recordarme);
CREATE INDEX idx_usuario_token_recuperacion ON usuario (token_recuperacion);

-- ============================================================================
-- 2. CLIENTES Y PROVEEDORES
-- ============================================================================

CREATE TABLE tipo_clipro (
    id_tipoclipro INT NOT NULL AUTO_INCREMENT,
    tipo VARCHAR(50) NOT NULL,
    PRIMARY KEY (id_tipoclipro),
    CONSTRAINT uq_tipo_clipro_tipo UNIQUE (tipo)
) ENGINE = InnoDB;

CREATE TABLE clipro (
    id_clipro INT NOT NULL AUTO_INCREMENT,
    id_tipoclipro INT NOT NULL,
    nombre VARCHAR(150) NOT NULL,
    nit VARCHAR(20) NULL,
    direccion VARCHAR(255) NULL,
    nombre_empresa VARCHAR(150) NULL,
    numero_empresa VARCHAR(20) NULL,
    telefono VARCHAR(20) NOT NULL,
    correo VARCHAR(150) NULL,
    clave VARCHAR(255) NULL,
    token_recordarme VARCHAR(255) NULL,
    fecha_expiracion_recordarme DATETIME NULL,
    token_recuperacion VARCHAR(200) NULL,
    fecha_expiracion_recuperacion DATETIME NULL,
    estado ENUM('activo', 'inactivo') NOT NULL DEFAULT 'activo',
    PRIMARY KEY (id_clipro),
    CONSTRAINT fk_clipro_tipo_clipro
        FOREIGN KEY (id_tipoclipro)
        REFERENCES tipo_clipro (id_tipoclipro)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE = InnoDB;

CREATE INDEX idx_clipro_id_tipoclipro ON clipro (id_tipoclipro);
CREATE INDEX idx_clipro_nombre ON clipro (nombre);
CREATE INDEX idx_clipro_nit ON clipro (nit);
CREATE INDEX idx_clipro_correo ON clipro (correo);
CREATE INDEX idx_clipro_telefono ON clipro (telefono);
CREATE INDEX idx_clipro_estado ON clipro (estado);
CREATE INDEX idx_clipro_token_recordarme ON clipro (token_recordarme);
CREATE INDEX idx_clipro_token_recuperacion ON clipro (token_recuperacion);

-- ============================================================================
-- 3. CATÁLOGOS GENERALES
-- ============================================================================

CREATE TABLE forma_pago (
    id_formapago INT NOT NULL AUTO_INCREMENT,
    nombre_forma VARCHAR(50) NOT NULL,
    PRIMARY KEY (id_formapago),
    CONSTRAINT uq_forma_pago_nombre UNIQUE (nombre_forma)
) ENGINE = InnoDB;

CREATE TABLE tipo_movimiento (
    id_tipomov INT NOT NULL AUTO_INCREMENT,
    nombre_tipomov VARCHAR(50) NOT NULL,
    PRIMARY KEY (id_tipomov),
    CONSTRAINT uq_tipo_movimiento_nombre UNIQUE (nombre_tipomov)
) ENGINE = InnoDB;

CREATE TABLE categoria (
    id_categoria INT NOT NULL AUTO_INCREMENT,
    nombre_categoria VARCHAR(100) NOT NULL,
    estado ENUM('activo', 'inactivo') NOT NULL DEFAULT 'activo',
    es_sistema TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (id_categoria),
    CONSTRAINT uq_categoria_nombre UNIQUE (nombre_categoria)
) ENGINE = InnoDB;

CREATE TABLE subcategoria (
    id_subcategoria INT NOT NULL AUTO_INCREMENT,
    id_categoria INT NOT NULL,
    nombre_subcategoria VARCHAR(100) NOT NULL,
    estado ENUM('activo', 'inactivo') NOT NULL DEFAULT 'activo',
    precio DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    PRIMARY KEY (id_subcategoria),
    CONSTRAINT uq_subcategoria_nombre UNIQUE (nombre_subcategoria),
    CONSTRAINT fk_subcategoria_categoria
        FOREIGN KEY (id_categoria)
        REFERENCES categoria (id_categoria)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE = InnoDB;

CREATE TABLE marca (
    id_marca INT NOT NULL AUTO_INCREMENT,
    nombre_marca VARCHAR(100) NOT NULL,
    estado ENUM('activo', 'inactivo') NOT NULL DEFAULT 'activo',
    PRIMARY KEY (id_marca),
    CONSTRAINT uq_marca_nombre UNIQUE (nombre_marca)
) ENGINE = InnoDB;

CREATE TABLE unidad_medida (
    id_umedida INT NOT NULL AUTO_INCREMENT,
    nombre VARCHAR(50) NOT NULL,
    PRIMARY KEY (id_umedida),
    CONSTRAINT uq_unidad_medida_nombre UNIQUE (nombre)
) ENGINE = InnoDB;

CREATE TABLE tipo_estado (
    id_tipoestado INT NOT NULL AUTO_INCREMENT,
    estado VARCHAR(50) NOT NULL,
    PRIMARY KEY (id_tipoestado),
    CONSTRAINT uq_tipo_estado_nombre UNIQUE (estado)
) ENGINE = InnoDB;

CREATE TABLE tipo_proser (
    id_tipoproser INT NOT NULL AUTO_INCREMENT,
    nombre VARCHAR(50) NOT NULL,
    PRIMARY KEY (id_tipoproser),
    CONSTRAINT uq_tipo_proser_nombre UNIQUE (nombre)
) ENGINE = InnoDB;

CREATE INDEX idx_categoria_estado ON categoria (estado);
CREATE INDEX idx_categoria_es_sistema ON categoria (es_sistema);
CREATE INDEX idx_subcategoria_id_categoria ON subcategoria (id_categoria);
CREATE INDEX idx_subcategoria_estado ON subcategoria (estado);
CREATE INDEX idx_marca_estado ON marca (estado);

-- ============================================================================
-- 4. PRODUCTOS, SERVICIOS Y HABITACIONES
-- Las habitaciones no poseen una tabla independiente: se almacenan en proser.
-- ============================================================================

CREATE TABLE proser (
    id_proser INT NOT NULL AUTO_INCREMENT,
    id_categoria INT NULL,
    id_subcategoria INT NULL,
    id_marca INT NULL,
    id_umedida INT NOT NULL,
    id_tipoestado INT NOT NULL,
    id_tipoproser INT NOT NULL,
    codigo VARCHAR(50) NOT NULL,
    nombre_proser VARCHAR(150) NOT NULL,
    precio DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    stock INT NOT NULL DEFAULT 0,
    descripcion VARCHAR(255) NULL,
    PRIMARY KEY (id_proser),
    CONSTRAINT uq_proser_codigo UNIQUE (codigo),
    CONSTRAINT fk_proser_categoria
        FOREIGN KEY (id_categoria)
        REFERENCES categoria (id_categoria)
        ON UPDATE CASCADE
        ON DELETE SET NULL,
    CONSTRAINT fk_proser_subcategoria
        FOREIGN KEY (id_subcategoria)
        REFERENCES subcategoria (id_subcategoria)
        ON UPDATE CASCADE
        ON DELETE SET NULL,
    CONSTRAINT fk_proser_marca
        FOREIGN KEY (id_marca)
        REFERENCES marca (id_marca)
        ON UPDATE CASCADE
        ON DELETE SET NULL,
    CONSTRAINT fk_proser_unidad_medida
        FOREIGN KEY (id_umedida)
        REFERENCES unidad_medida (id_umedida)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT fk_proser_tipo_estado
        FOREIGN KEY (id_tipoestado)
        REFERENCES tipo_estado (id_tipoestado)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT fk_proser_tipo_proser
        FOREIGN KEY (id_tipoproser)
        REFERENCES tipo_proser (id_tipoproser)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT chk_proser_precio_no_negativo CHECK (precio >= 0),
    CONSTRAINT chk_proser_stock_no_negativo CHECK (stock >= 0)
) ENGINE = InnoDB;

CREATE INDEX idx_proser_id_categoria ON proser (id_categoria);
CREATE INDEX idx_proser_id_subcategoria ON proser (id_subcategoria);
CREATE INDEX idx_proser_id_marca ON proser (id_marca);
CREATE INDEX idx_proser_id_umedida ON proser (id_umedida);
CREATE INDEX idx_proser_id_tipoestado ON proser (id_tipoestado);
CREATE INDEX idx_proser_id_tipoproser ON proser (id_tipoproser);
CREATE INDEX idx_proser_nombre ON proser (nombre_proser);

-- ============================================================================
-- 5. RESERVAS
-- ============================================================================

CREATE TABLE reserva (
    id_reserva INT NOT NULL AUTO_INCREMENT,
    id_clipro INT NOT NULL,
    id_habitacion INT NOT NULL,
    precio_noche_aplicado DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    fecha_reserva DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_entrada DATE NOT NULL,
    fecha_salida DATE NOT NULL,
    cantidad_personas INT NOT NULL DEFAULT 1,
    total_reserva DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    saldo_pendiente DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    estado ENUM(
        'pendiente',
        'confirmada',
        'en_curso',
        'cancelada',
        'finalizada'
    ) NOT NULL DEFAULT 'pendiente',
    codigo_seguridad VARCHAR(50) NULL,
    observaciones VARCHAR(255) NULL,
    PRIMARY KEY (id_reserva),
    CONSTRAINT uq_reserva_codigo_seguridad UNIQUE (codigo_seguridad),
    CONSTRAINT fk_reserva_clipro
        FOREIGN KEY (id_clipro)
        REFERENCES clipro (id_clipro)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT fk_reserva_habitacion
        FOREIGN KEY (id_habitacion)
        REFERENCES proser (id_proser)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT chk_reserva_fechas CHECK (fecha_salida > fecha_entrada),
    CONSTRAINT chk_reserva_cantidad_personas CHECK (cantidad_personas > 0),
    CONSTRAINT chk_reserva_precio_noche CHECK (precio_noche_aplicado >= 0),
    CONSTRAINT chk_reserva_total CHECK (total_reserva >= 0),
    CONSTRAINT chk_reserva_saldo CHECK (saldo_pendiente >= 0)
) ENGINE = InnoDB;

CREATE INDEX idx_reserva_id_clipro ON reserva (id_clipro);
CREATE INDEX idx_reserva_id_habitacion ON reserva (id_habitacion);
CREATE INDEX idx_reserva_fecha_reserva ON reserva (fecha_reserva);
CREATE INDEX idx_reserva_fecha_entrada ON reserva (fecha_entrada);
CREATE INDEX idx_reserva_fecha_salida ON reserva (fecha_salida);
CREATE INDEX idx_reserva_estado ON reserva (estado);

-- ============================================================================
-- 6. MOVIMIENTOS FINANCIEROS Y DETALLE
-- ============================================================================

CREATE TABLE movimiento (
    id_movimiento INT NOT NULL AUTO_INCREMENT,
    id_usuario INT NOT NULL,
    id_clipro INT NOT NULL,
    id_tipomov INT NOT NULL,
    id_formapago INT NOT NULL,
    id_reserva INT NULL,
    fecha_hora DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    estado ENUM('activo', 'anulado') NOT NULL DEFAULT 'activo',
    observaciones VARCHAR(255) NULL,
    PRIMARY KEY (id_movimiento),
    CONSTRAINT fk_movimiento_usuario
        FOREIGN KEY (id_usuario)
        REFERENCES usuario (id_usuario)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT fk_movimiento_clipro
        FOREIGN KEY (id_clipro)
        REFERENCES clipro (id_clipro)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT fk_movimiento_tipo_movimiento
        FOREIGN KEY (id_tipomov)
        REFERENCES tipo_movimiento (id_tipomov)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT fk_movimiento_forma_pago
        FOREIGN KEY (id_formapago)
        REFERENCES forma_pago (id_formapago)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT fk_movimiento_reserva
        FOREIGN KEY (id_reserva)
        REFERENCES reserva (id_reserva)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE = InnoDB;

CREATE TABLE detalle (
    id_detalle INT NOT NULL AUTO_INCREMENT,
    id_movimiento INT NOT NULL,
    -- Es NULL en detalles financieros que representan pagos o cuentas por
    -- cobrar sin corresponder a un producto, servicio o habitación concreto.
    id_proser INT NULL,
    cantidad INT NOT NULL DEFAULT 1,
    precio_unitario DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    subtotal DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    descripcion VARCHAR(255) NULL,
    PRIMARY KEY (id_detalle),
    CONSTRAINT fk_detalle_movimiento
        FOREIGN KEY (id_movimiento)
        REFERENCES movimiento (id_movimiento)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT fk_detalle_proser
        FOREIGN KEY (id_proser)
        REFERENCES proser (id_proser)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT chk_detalle_cantidad CHECK (cantidad > 0),
    CONSTRAINT chk_detalle_precio CHECK (precio_unitario >= 0),
    CONSTRAINT chk_detalle_subtotal CHECK (subtotal >= 0)
) ENGINE = InnoDB;

CREATE INDEX idx_movimiento_id_usuario ON movimiento (id_usuario);
CREATE INDEX idx_movimiento_id_clipro ON movimiento (id_clipro);
CREATE INDEX idx_movimiento_id_tipomov ON movimiento (id_tipomov);
CREATE INDEX idx_movimiento_id_formapago ON movimiento (id_formapago);
CREATE INDEX idx_movimiento_id_reserva ON movimiento (id_reserva);
CREATE INDEX idx_movimiento_fecha_hora ON movimiento (fecha_hora);
CREATE INDEX idx_movimiento_estado ON movimiento (estado);

CREATE INDEX idx_detalle_id_movimiento ON detalle (id_movimiento);
CREATE INDEX idx_detalle_id_proser ON detalle (id_proser);

-- ============================================================================
-- 7. DATOS INICIALES DE SEGURIDAD
-- ============================================================================

INSERT INTO rol (id_rol, nombre_rol, estado) VALUES
    (1, 'admin', 'activo'),
    (2, 'recepcionista', 'activo'),
    (3, 'camarero', 'activo'),
    (4, 'cliente', 'activo');

INSERT INTO permisos (id_permiso, nombre_permiso, descripcion, estado) VALUES
    (1,  'crear_usuario', 'Permite crear usuarios administrativos', 1),
    (2,  'editar_usuario', 'Permite editar usuarios administrativos', 1),
    (3,  'eliminar_usuario', 'Permite eliminar usuarios administrativos', 1),
    (4,  'ver_usuarios', 'Permite consultar usuarios administrativos', 1),
    (5,  'cambiar_estado_usuario', 'Permite activar o inactivar usuarios', 1),
    (6,  'resetear_clave_usuario', 'Permite restablecer la contraseña de usuarios', 1),
    (7,  'gestionar_roles', 'Permite administrar roles', 1),
    (8,  'gestionar_permisos', 'Permite administrar permisos y sus asignaciones', 1),
    (9,  'registrar_venta', 'Permite registrar ventas en el Punto de Venta', 1),
    (10, 'facturar', 'Permite generar facturas', 1),
    (11, 'gestionar_inventario', 'Permite administrar y consultar inventario', 1),
    (12, 'gestionar_reservas', 'Permite administrar reservaciones', 1),
    (13, 'registrar_checkin', 'Permite registrar el ingreso de huéspedes', 1),
    (14, 'registrar_checkout', 'Permite registrar la salida de huéspedes', 1),
    (15, 'gestionar_cxc', 'Permite administrar cuentas por cobrar', 1),
    (16, 'gestionar_cxp', 'Permite administrar cuentas por pagar', 1),
    (17, 'realizar_reserva_online', 'Permite realizar reservaciones en línea', 1),
    (18, 'pagar_anticipo', 'Permite registrar pagos de reservaciones', 1),
    (19, 'ver_reservas', 'Permite consultar reservaciones', 1),
    (20, 'crear_reserva', 'Permite crear reservaciones', 1),
    (21, 'editar_reserva', 'Permite editar reservaciones', 1),
    (22, 'cancelar_reserva', 'Permite cancelar reservaciones', 1),
    (23, 'checkin', 'Permite ejecutar el check-in de una reservación', 1),
    (24, 'checkout', 'Permite ejecutar el check-out de una reservación', 1),
    (25, 'ver_clientes', 'Permite consultar clientes', 1),
    (26, 'registrar_cliente', 'Permite registrar clientes', 1),
    (27, 'ver_habitaciones', 'Permite consultar habitaciones', 1),
    (28, 'ver_disponibilidad_habitaciones', 'Permite consultar disponibilidad de habitaciones', 1),
    (29, 'ver_menu', 'Permite consultar productos y servicios disponibles', 1),
    (30, 'registrar_consumo', 'Permite registrar consumos', 1),
    (31, 'cobrar_cuenta', 'Permite cobrar cuentas', 1),
    (32, 'ver_disponibilidad_productos', 'Permite consultar disponibilidad de productos', 1),
    (33, 'ver_reservas_propias', 'Permite al cliente consultar sus reservaciones', 1),
    (34, 'ver_pagos_propios', 'Permite al cliente consultar sus pagos', 1),
    (35, 'ver_saldos_pendientes', 'Permite al cliente consultar sus saldos pendientes', 1),
    (36, 'actualizar_datos_propios', 'Permite al cliente actualizar sus datos personales', 1);

-- El administrador recibe todos los permisos existentes.
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT 1, id_permiso
FROM permisos;

-- Permisos operativos del rol recepcionista.
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT 2, id_permiso
FROM permisos
WHERE nombre_permiso IN (
    'gestionar_reservas',
    'registrar_checkin',
    'registrar_checkout',
    'gestionar_cxc',
    'ver_reservas',
    'crear_reserva',
    'editar_reserva',
    'cancelar_reserva',
    'checkin',
    'checkout',
    'ver_clientes',
    'registrar_cliente',
    'ver_habitaciones',
    'ver_disponibilidad_habitaciones'
);

-- Permisos operativos del rol camarero.
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT 3, id_permiso
FROM permisos
WHERE nombre_permiso IN (
    'registrar_venta',
    'ver_menu',
    'registrar_consumo',
    'cobrar_cuenta',
    'ver_disponibilidad_productos'
);

-- Permisos del portal de clientes.
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT 4, id_permiso
FROM permisos
WHERE nombre_permiso IN (
    'realizar_reserva_online',
    'pagar_anticipo',
    'crear_reserva',
    'ver_reservas_propias',
    'ver_pagos_propios',
    'ver_saldos_pendientes',
    'actualizar_datos_propios'
);

-- ============================================================================
-- 8. DATOS INICIALES DE CATÁLOGOS
-- Se conservan IDs explícitos porque algunos flujos actuales los utilizan.
-- ============================================================================

INSERT INTO tipo_clipro (id_tipoclipro, tipo) VALUES
    (1, 'cliente'),
    (2, 'proveedor');

INSERT INTO forma_pago (id_formapago, nombre_forma) VALUES
    (1, 'efectivo'),
    (2, 'deposito'),
    (3, 'transferencia'),
    (4, 'tarjeta'),
    (5, 'credito');

INSERT INTO tipo_movimiento (id_tipomov, nombre_tipomov) VALUES
    (1, 'venta'),
    (2, 'compra'),
    (3, 'reserva'),
    (4, 'anticipo_reserva'),
    (5, 'cuenta_por_cobrar'),
    (6, 'cuenta_por_pagar');

INSERT INTO categoria (id_categoria, nombre_categoria, estado, es_sistema) VALUES
    (1, 'lacteos', 'activo', 0),
    (2, 'bebidas', 'activo', 0),
    (3, 'abarrotes', 'activo', 0),
    (4, 'limpieza', 'activo', 0),
    (5, 'Habitaciones', 'activo', 1);

INSERT INTO subcategoria (
    id_subcategoria,
    id_categoria,
    nombre_subcategoria,
    estado,
    precio
) VALUES
    (1, 1, 'leche', 'activo', 0.00),
    (2, 1, 'queso', 'activo', 0.00),
    (3, 2, 'gaseosas', 'activo', 0.00),
    (4, 2, 'agua_pura', 'activo', 0.00),
    (5, 4, 'detergente', 'activo', 0.00),
    (6, 5, 'Sencilla', 'activo', 0.00),
    (7, 5, 'Doble', 'activo', 0.00),
    (8, 5, 'Suite', 'activo', 0.00),
    (9, 5, 'Familiar', 'activo', 0.00);

INSERT INTO marca (id_marca, nombre_marca, estado) VALUES
    (1, 'generica', 'activo');

INSERT INTO unidad_medida (id_umedida, nombre) VALUES
    (1, 'unidad'),
    (2, 'servicio'),
    (3, 'noche'),
    (4, 'litro'),
    (5, 'kilo');

INSERT INTO tipo_estado (id_tipoestado, estado) VALUES
    (1, 'remodelacion'),
    (2, 'renta'),
    (3, 'libre'),
    (4, 'ocupada'),
    (5, 'activo'),
    (6, 'inactivo');

INSERT INTO tipo_proser (id_tipoproser, nombre) VALUES
    (1, 'producto'),
    (2, 'servicio'),
    (3, 'habitacion');

-- ============================================================================
-- 9. REGISTROS TÉCNICOS INICIALES
-- ============================================================================

-- Usuario administrador inicial.
-- Correo: admin@hotel.com
-- Contraseña inicial: Admin123
-- Debe cambiarse después de la primera instalación.
INSERT INTO usuario (
    id_usuario,
    id_rol,
    nombre_usuario,
    correo,
    telefono,
    clave,
    estado
) VALUES (
    1,
    1,
    'Administrador',
    'admin@hotel.com',
    '00000000',
    SHA2('Admin123', 256),
    'activo'
);

-- Cliente técnico utilizado por el POS en ventas sin huésped o reserva.
-- Se crea completo porque el código actual requiere que ya exista.
INSERT INTO clipro (
    id_clipro,
    id_tipoclipro,
    nombre,
    telefono,
    estado
) VALUES (
    1,
    1,
    'CLIENTE GENERAL',
    '00000000',
    'activo'
);

-- Servicios básicos del hotel.
INSERT INTO proser (
    id_proser,
    id_categoria,
    id_subcategoria,
    id_marca,
    id_umedida,
    id_tipoestado,
    id_tipoproser,
    codigo,
    nombre_proser,
    precio,
    stock,
    descripcion
) VALUES
    (
        1,
        NULL,
        NULL,
        1,
        2,
        5,
        2,
        'SER001',
        'Limpieza',
        0.00,
        0,
        'Servicio de limpieza'
    ),
    (
        2,
        NULL,
        NULL,
        1,
        2,
        5,
        2,
        'SER002',
        'Lavanderia',
        0.00,
        0,
        'Servicio de lavanderia'
    );