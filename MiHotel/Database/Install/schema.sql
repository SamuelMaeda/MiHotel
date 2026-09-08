
/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;
DROP TABLE IF EXISTS `categoria`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `categoria` (
  `id_categoria` int NOT NULL AUTO_INCREMENT,
  `nombre_categoria` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `estado` enum('activo','inactivo') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'activo',
  `es_sistema` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id_categoria`),
  UNIQUE KEY `uq_categoria_nombre` (`nombre_categoria`),
  KEY `idx_categoria_estado` (`estado`),
  KEY `idx_categoria_es_sistema` (`es_sistema`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `clasificacion_cliente`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `clasificacion_cliente` (
  `codigo` char(1) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nombre` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`codigo`),
  UNIQUE KEY `uq_clasificacion_cliente_nombre` (`nombre`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `cliente_detalle`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cliente_detalle` (
  `id_clipro` int NOT NULL,
  `id_empresa_cliente` int DEFAULT NULL,
  `numero_dpi` char(13) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `placa_reciente` varchar(15) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `codigo_clasificacion` char(1) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'B',
  `solicita_limpieza` tinyint(1) NOT NULL DEFAULT '1',
  PRIMARY KEY (`id_clipro`),
  UNIQUE KEY `uq_cliente_detalle_dpi` (`numero_dpi`),
  KEY `idx_cliente_detalle_empresa` (`id_empresa_cliente`),
  KEY `idx_cliente_detalle_clasificacion` (`codigo_clasificacion`),
  KEY `idx_cliente_detalle_placa` (`placa_reciente`),
  CONSTRAINT `fk_cliente_detalle_clasificacion` FOREIGN KEY (`codigo_clasificacion`) REFERENCES `clasificacion_cliente` (`codigo`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_cliente_detalle_clipro` FOREIGN KEY (`id_clipro`) REFERENCES `clipro` (`id_clipro`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_cliente_detalle_empresa` FOREIGN KEY (`id_empresa_cliente`) REFERENCES `empresa_cliente` (`id_empresa_cliente`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `cliente_documento`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cliente_documento` (
  `id_cliente_documento` int NOT NULL AUTO_INCREMENT,
  `id_clipro` int NOT NULL,
  `tipo_documento` enum('dpi_frente','dpi_reverso') COLLATE utf8mb4_unicode_ci NOT NULL,
  `contenido` mediumblob NOT NULL,
  `tipo_mime` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'image/jpeg',
  `nombre_original` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `tamano_original` bigint unsigned DEFAULT NULL,
  `tamano_comprimido` bigint unsigned DEFAULT NULL,
  `fecha_subida` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id_cliente_documento`),
  UNIQUE KEY `uq_cliente_documento_tipo` (`id_clipro`,`tipo_documento`),
  KEY `idx_cliente_documento_cliente` (`id_clipro`),
  CONSTRAINT `fk_cliente_documento_clipro` FOREIGN KEY (`id_clipro`) REFERENCES `clipro` (`id_clipro`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `clipro`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `clipro` (
  `id_clipro` int NOT NULL AUTO_INCREMENT,
  `id_tipoclipro` int NOT NULL,
  `nombre` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nit` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `direccion` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `nombre_empresa` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `telefono` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `correo` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `clave` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `token_recordarme` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `fecha_expiracion_recordarme` datetime DEFAULT NULL,
  `token_recuperacion` varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `fecha_expiracion_recuperacion` datetime DEFAULT NULL,
  `estado` enum('activo','inactivo') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'activo',
  PRIMARY KEY (`id_clipro`),
  KEY `idx_clipro_id_tipoclipro` (`id_tipoclipro`),
  KEY `idx_clipro_nombre` (`nombre`),
  KEY `idx_clipro_nit` (`nit`),
  KEY `idx_clipro_correo` (`correo`),
  KEY `idx_clipro_telefono` (`telefono`),
  KEY `idx_clipro_estado` (`estado`),
  KEY `idx_clipro_token_recordarme` (`token_recordarme`),
  KEY `idx_clipro_token_recuperacion` (`token_recuperacion`),
  CONSTRAINT `fk_clipro_tipo_clipro` FOREIGN KEY (`id_tipoclipro`) REFERENCES `tipo_clipro` (`id_tipoclipro`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `configuracion_sistema`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `configuracion_sistema` (
  `id_configuracion` tinyint unsigned NOT NULL,
  `recargo_tarjeta` decimal(10,2) NOT NULL DEFAULT '25.00',
  `iva_porcentaje` decimal(5,2) NOT NULL DEFAULT '12.00',
  `impuesto_turismo_porcentaje` decimal(5,2) NOT NULL DEFAULT '10.00',
  `fecha_actualizacion` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `id_usuario_actualizacion` int DEFAULT NULL,
  PRIMARY KEY (`id_configuracion`),
  KEY `fk_configuracion_usuario` (`id_usuario_actualizacion`),
  CONSTRAINT `fk_configuracion_usuario` FOREIGN KEY (`id_usuario_actualizacion`) REFERENCES `usuario` (`id_usuario`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `cuenta_por_pagar`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cuenta_por_pagar` (
  `id_cuenta_por_pagar` int NOT NULL AUTO_INCREMENT,
  `nombre` varchar(150) NOT NULL,
  `descripcion` varchar(1000) DEFAULT NULL,
  `tipo_monto` varchar(20) NOT NULL DEFAULT 'fijo',
  `monto_mensual` decimal(12,2) DEFAULT NULL,
  `dia_vencimiento` tinyint unsigned NOT NULL DEFAULT '1',
  `activa` tinyint(1) NOT NULL DEFAULT '1',
  `proximo_periodo` date NOT NULL,
  `fecha_registro` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `fecha_modificacion` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `fecha_desactivacion` datetime DEFAULT NULL,
  PRIMARY KEY (`id_cuenta_por_pagar`),
  KEY `ix_cuenta_por_pagar_activa` (`activa`),
  KEY `ix_cuenta_por_pagar_proximo_periodo` (`proximo_periodo`),
  KEY `ix_cuenta_por_pagar_nombre` (`nombre`),
  KEY `ix_cuenta_por_pagar_tipo` (`tipo_monto`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `cuenta_por_pagar_periodo`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cuenta_por_pagar_periodo` (
  `id_periodo` bigint NOT NULL AUTO_INCREMENT,
  `id_cuenta_por_pagar` int NOT NULL,
  `periodo` date NOT NULL,
  `monto_generado` decimal(12,2) DEFAULT NULL,
  `estado` varchar(20) NOT NULL DEFAULT 'pendiente',
  `fecha_generacion` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `fecha_pago` datetime DEFAULT NULL,
  PRIMARY KEY (`id_periodo`),
  UNIQUE KEY `uq_cuenta_por_pagar_periodo` (`id_cuenta_por_pagar`,`periodo`),
  KEY `ix_cuenta_periodo_estado` (`id_cuenta_por_pagar`,`estado`),
  CONSTRAINT `fk_cuenta_periodo_cuenta` FOREIGN KEY (`id_cuenta_por_pagar`) REFERENCES `cuenta_por_pagar` (`id_cuenta_por_pagar`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `detalle`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `detalle` (
  `id_detalle` int NOT NULL AUTO_INCREMENT,
  `id_movimiento` int NOT NULL,
  `id_proser` int DEFAULT NULL,
  `cantidad` int NOT NULL DEFAULT '1',
  `precio_unitario` decimal(10,2) NOT NULL DEFAULT '0.00',
  `subtotal` decimal(10,2) NOT NULL DEFAULT '0.00',
  `descripcion` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id_detalle`),
  KEY `idx_detalle_id_movimiento` (`id_movimiento`),
  KEY `idx_detalle_id_proser` (`id_proser`),
  CONSTRAINT `fk_detalle_movimiento` FOREIGN KEY (`id_movimiento`) REFERENCES `movimiento` (`id_movimiento`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_detalle_proser` FOREIGN KEY (`id_proser`) REFERENCES `proser` (`id_proser`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `chk_detalle_cantidad` CHECK ((`cantidad` > 0)),
  CONSTRAINT `chk_detalle_precio` CHECK ((`precio_unitario` >= 0)),
  CONSTRAINT `chk_detalle_subtotal` CHECK ((`subtotal` >= 0))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `documento_fiscal`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `documento_fiscal` (
  `id_documento_fiscal` bigint NOT NULL AUTO_INCREMENT,
  `tipo_documento` enum('factura','nota_credito','nota_debito') NOT NULL DEFAULT 'factura',
  `nit_receptor` varchar(40) DEFAULT NULL,
  `serie` varchar(50) DEFAULT NULL,
  `numero_dte` varchar(50) DEFAULT NULL,
  `contenido` longblob NOT NULL,
  `tipo_mime` varchar(100) NOT NULL DEFAULT 'application/pdf',
  `nombre_original` varchar(255) NOT NULL,
  `tamano` bigint unsigned NOT NULL,
  `estado` enum('vigente','anulado','sustituido') NOT NULL DEFAULT 'vigente',
  `id_documento_origen` bigint DEFAULT NULL,
  `id_reserva_factura_legacy` int DEFAULT NULL,
  `id_usuario_registro` int NOT NULL,
  `fecha_registro` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `id_usuario_estado` int DEFAULT NULL,
  `fecha_estado` datetime DEFAULT NULL,
  `motivo_estado` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id_documento_fiscal`),
  UNIQUE KEY `uq_documento_serie_numero` (`serie`,`numero_dte`),
  UNIQUE KEY `uq_documento_legacy` (`id_reserva_factura_legacy`),
  KEY `ix_documento_nit` (`nit_receptor`),
  KEY `ix_documento_estado` (`estado`),
  KEY `ix_documento_usuario` (`id_usuario_registro`),
  KEY `fk_documento_usuario_estado` (`id_usuario_estado`),
  KEY `fk_documento_origen` (`id_documento_origen`),
  CONSTRAINT `fk_documento_origen` FOREIGN KEY (`id_documento_origen`) REFERENCES `documento_fiscal` (`id_documento_fiscal`),
  CONSTRAINT `fk_documento_usuario_estado` FOREIGN KEY (`id_usuario_estado`) REFERENCES `usuario` (`id_usuario`),
  CONSTRAINT `fk_documento_usuario_registro` FOREIGN KEY (`id_usuario_registro`) REFERENCES `usuario` (`id_usuario`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `documento_fiscal_reserva`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `documento_fiscal_reserva` (
  `id_documento_fiscal` bigint NOT NULL,
  `id_reserva` int NOT NULL,
  PRIMARY KEY (`id_documento_fiscal`,`id_reserva`),
  KEY `ix_documento_reserva_reserva` (`id_reserva`),
  CONSTRAINT `fk_documento_reserva_documento` FOREIGN KEY (`id_documento_fiscal`) REFERENCES `documento_fiscal` (`id_documento_fiscal`),
  CONSTRAINT `fk_documento_reserva_reserva` FOREIGN KEY (`id_reserva`) REFERENCES `reserva` (`id_reserva`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `empresa_cliente`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `empresa_cliente` (
  `id_empresa_cliente` int NOT NULL AUTO_INCREMENT,
  `nombre` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `estado` enum('activo','inactivo') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'activo',
  PRIMARY KEY (`id_empresa_cliente`),
  UNIQUE KEY `uq_empresa_cliente_nombre` (`nombre`),
  KEY `idx_empresa_cliente_estado` (`estado`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `forma_pago`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `forma_pago` (
  `id_formapago` int NOT NULL AUTO_INCREMENT,
  `nombre_forma` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`id_formapago`),
  UNIQUE KEY `uq_forma_pago_nombre` (`nombre_forma`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `habitacion_fotografia`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `habitacion_fotografia` (
  `id_fotografia` int NOT NULL AUTO_INCREMENT,
  `id_proser` int NOT NULL,
  `contenido` longblob NOT NULL,
  `tipo_mime` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'image/jpeg',
  `nombre_original` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `tamano_original` bigint unsigned NOT NULL,
  `tamano_comprimido` bigint unsigned NOT NULL,
  `fecha_subida` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id_fotografia`),
  KEY `idx_habitacion_fotografia_proser` (`id_proser`),
  CONSTRAINT `fk_habitacion_fotografia_proser` FOREIGN KEY (`id_proser`) REFERENCES `proser` (`id_proser`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `mantenimiento`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `mantenimiento` (
  `id_mantenimiento` int NOT NULL AUTO_INCREMENT,
  `nombre` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `descripcion` varchar(1000) COLLATE utf8mb4_unicode_ci NOT NULL,
  `fecha_registro` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `fecha_completado` datetime DEFAULT NULL,
  `estado` enum('pendiente','completado') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'pendiente',
  PRIMARY KEY (`id_mantenimiento`),
  KEY `idx_mantenimiento_estado` (`estado`),
  KEY `idx_mantenimiento_fecha_registro` (`fecha_registro`),
  KEY `idx_mantenimiento_fecha_completado` (`fecha_completado`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `marca`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `marca` (
  `id_marca` int NOT NULL AUTO_INCREMENT,
  `nombre_marca` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `estado` enum('activo','inactivo') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'activo',
  PRIMARY KEY (`id_marca`),
  UNIQUE KEY `uq_marca_nombre` (`nombre_marca`),
  KEY `idx_marca_estado` (`estado`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `movimiento`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `movimiento` (
  `id_movimiento` int NOT NULL AUTO_INCREMENT,
  `id_usuario` int NOT NULL,
  `id_clipro` int NOT NULL,
  `id_tipomov` int NOT NULL,
  `id_formapago` int DEFAULT NULL,
  `id_reserva` int DEFAULT NULL,
  `id_reserva_grupo` int DEFAULT NULL,
  `recargo_tarjeta` decimal(10,2) NOT NULL DEFAULT '0.00',
  `fecha_hora` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `estado` enum('activo','anulado') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'activo',
  `observaciones` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id_movimiento`),
  KEY `idx_movimiento_id_usuario` (`id_usuario`),
  KEY `idx_movimiento_id_clipro` (`id_clipro`),
  KEY `idx_movimiento_id_tipomov` (`id_tipomov`),
  KEY `idx_movimiento_id_formapago` (`id_formapago`),
  KEY `idx_movimiento_id_reserva` (`id_reserva`),
  KEY `idx_movimiento_fecha_hora` (`fecha_hora`),
  KEY `idx_movimiento_estado` (`estado`),
  KEY `idx_movimiento_id_reserva_grupo` (`id_reserva_grupo`),
  CONSTRAINT `fk_movimiento_clipro` FOREIGN KEY (`id_clipro`) REFERENCES `clipro` (`id_clipro`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_movimiento_forma_pago` FOREIGN KEY (`id_formapago`) REFERENCES `forma_pago` (`id_formapago`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_movimiento_reserva` FOREIGN KEY (`id_reserva`) REFERENCES `reserva` (`id_reserva`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_movimiento_reserva_grupo` FOREIGN KEY (`id_reserva_grupo`) REFERENCES `reserva_grupo` (`id_reserva_grupo`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_movimiento_tipo_movimiento` FOREIGN KEY (`id_tipomov`) REFERENCES `tipo_movimiento` (`id_tipomov`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_movimiento_usuario` FOREIGN KEY (`id_usuario`) REFERENCES `usuario` (`id_usuario`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `movimiento_reserva_aplicacion`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `movimiento_reserva_aplicacion` (
  `id_movimiento` int NOT NULL,
  `id_reserva` int NOT NULL,
  `monto` decimal(10,2) NOT NULL,
  PRIMARY KEY (`id_movimiento`,`id_reserva`),
  KEY `idx_movimiento_aplicacion_reserva` (`id_reserva`),
  CONSTRAINT `fk_movimiento_aplicacion_movimiento` FOREIGN KEY (`id_movimiento`) REFERENCES `movimiento` (`id_movimiento`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_movimiento_aplicacion_reserva` FOREIGN KEY (`id_reserva`) REFERENCES `reserva` (`id_reserva`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `pago_cuenta_por_pagar`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `pago_cuenta_por_pagar` (
  `id_pago_cuenta_por_pagar` bigint NOT NULL AUTO_INCREMENT,
  `id_cuenta_por_pagar` int NOT NULL,
  `monto` decimal(12,2) NOT NULL,
  `periodos_cubiertos` int unsigned NOT NULL,
  `fecha_pago` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `id_usuario` int NOT NULL,
  `observaciones` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`id_pago_cuenta_por_pagar`),
  KEY `ix_pago_cuenta_fecha` (`id_cuenta_por_pagar`,`fecha_pago`),
  KEY `ix_pago_cuenta_usuario` (`id_usuario`),
  CONSTRAINT `fk_pago_cuenta_cuenta` FOREIGN KEY (`id_cuenta_por_pagar`) REFERENCES `cuenta_por_pagar` (`id_cuenta_por_pagar`),
  CONSTRAINT `fk_pago_cuenta_usuario` FOREIGN KEY (`id_usuario`) REFERENCES `usuario` (`id_usuario`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `permisos`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `permisos` (
  `id_permiso` int NOT NULL AUTO_INCREMENT,
  `nombre_permiso` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `descripcion` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `estado` tinyint(1) NOT NULL DEFAULT '1',
  PRIMARY KEY (`id_permiso`),
  UNIQUE KEY `uq_permisos_nombre` (`nombre_permiso`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `proser`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `proser` (
  `id_proser` int NOT NULL AUTO_INCREMENT,
  `id_categoria` int DEFAULT NULL,
  `id_subcategoria` int DEFAULT NULL,
  `id_marca` int DEFAULT NULL,
  `id_umedida` int NOT NULL,
  `id_tipoestado` int NOT NULL,
  `id_tipoproser` int NOT NULL,
  `codigo` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nombre_proser` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `precio` decimal(10,2) NOT NULL DEFAULT '0.00',
  `stock` int NOT NULL DEFAULT '0',
  `descripcion` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id_proser`),
  UNIQUE KEY `uq_proser_codigo` (`codigo`),
  KEY `idx_proser_id_categoria` (`id_categoria`),
  KEY `idx_proser_id_subcategoria` (`id_subcategoria`),
  KEY `idx_proser_id_marca` (`id_marca`),
  KEY `idx_proser_id_umedida` (`id_umedida`),
  KEY `idx_proser_id_tipoestado` (`id_tipoestado`),
  KEY `idx_proser_id_tipoproser` (`id_tipoproser`),
  KEY `idx_proser_nombre` (`nombre_proser`),
  CONSTRAINT `fk_proser_categoria` FOREIGN KEY (`id_categoria`) REFERENCES `categoria` (`id_categoria`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_proser_marca` FOREIGN KEY (`id_marca`) REFERENCES `marca` (`id_marca`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_proser_subcategoria` FOREIGN KEY (`id_subcategoria`) REFERENCES `subcategoria` (`id_subcategoria`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_proser_tipo_estado` FOREIGN KEY (`id_tipoestado`) REFERENCES `tipo_estado` (`id_tipoestado`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_proser_tipo_proser` FOREIGN KEY (`id_tipoproser`) REFERENCES `tipo_proser` (`id_tipoproser`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_proser_unidad_medida` FOREIGN KEY (`id_umedida`) REFERENCES `unidad_medida` (`id_umedida`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `chk_proser_precio_no_negativo` CHECK ((`precio` >= 0)),
  CONSTRAINT `chk_proser_stock_no_negativo` CHECK ((`stock` >= 0))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `reserva`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `reserva` (
  `id_reserva` int NOT NULL AUTO_INCREMENT,
  `id_reserva_grupo` int DEFAULT NULL,
  `id_clipro` int NOT NULL,
  `id_habitacion` int NOT NULL,
  `precio_noche_aplicado` decimal(10,2) NOT NULL DEFAULT '0.00',
  `fecha_reserva` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `fecha_entrada` date NOT NULL,
  `fecha_salida` date NOT NULL,
  `fecha_hora_checkin` datetime DEFAULT NULL,
  `fecha_hora_checkout` datetime DEFAULT NULL,
  `cantidad_personas` int NOT NULL DEFAULT '1',
  `total_reserva` decimal(10,2) NOT NULL DEFAULT '0.00',
  `saldo_pendiente` decimal(10,2) NOT NULL DEFAULT '0.00',
  `estado` enum('pendiente','en_curso','en_checkout','cancelada','finalizada') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'pendiente',
  `codigo_seguridad` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `observaciones` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id_reserva`),
  UNIQUE KEY `uq_reserva_codigo_seguridad` (`codigo_seguridad`),
  KEY `idx_reserva_id_clipro` (`id_clipro`),
  KEY `idx_reserva_id_habitacion` (`id_habitacion`),
  KEY `idx_reserva_fecha_reserva` (`fecha_reserva`),
  KEY `idx_reserva_fecha_entrada` (`fecha_entrada`),
  KEY `idx_reserva_fecha_salida` (`fecha_salida`),
  KEY `idx_reserva_estado` (`estado`),
  KEY `idx_reserva_id_reserva_grupo` (`id_reserva_grupo`),
  CONSTRAINT `fk_reserva_clipro` FOREIGN KEY (`id_clipro`) REFERENCES `clipro` (`id_clipro`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_reserva_habitacion` FOREIGN KEY (`id_habitacion`) REFERENCES `proser` (`id_proser`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_reserva_reserva_grupo` FOREIGN KEY (`id_reserva_grupo`) REFERENCES `reserva_grupo` (`id_reserva_grupo`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `chk_reserva_cantidad_personas` CHECK ((`cantidad_personas` > 0)),
  CONSTRAINT `chk_reserva_fechas` CHECK ((`fecha_salida` > `fecha_entrada`)),
  CONSTRAINT `chk_reserva_precio_noche` CHECK ((`precio_noche_aplicado` >= 0)),
  CONSTRAINT `chk_reserva_saldo` CHECK ((`saldo_pendiente` >= 0)),
  CONSTRAINT `chk_reserva_total` CHECK ((`total_reserva` >= 0))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `reserva_factura`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `reserva_factura` (
  `id_reserva_factura` int NOT NULL AUTO_INCREMENT,
  `id_reserva` int DEFAULT NULL,
  `id_reserva_grupo` int DEFAULT NULL,
  `contenido` longblob NOT NULL,
  `tipo_mime` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'application/pdf',
  `nombre_original` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `tamano` bigint unsigned NOT NULL,
  `fecha_subida` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `id_usuario` int NOT NULL,
  PRIMARY KEY (`id_reserva_factura`),
  UNIQUE KEY `uq_reserva_factura_reserva` (`id_reserva`),
  UNIQUE KEY `uq_reserva_factura_grupo` (`id_reserva_grupo`),
  KEY `fk_reserva_factura_usuario` (`id_usuario`),
  CONSTRAINT `fk_reserva_factura_grupo` FOREIGN KEY (`id_reserva_grupo`) REFERENCES `reserva_grupo` (`id_reserva_grupo`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_reserva_factura_reserva` FOREIGN KEY (`id_reserva`) REFERENCES `reserva` (`id_reserva`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_reserva_factura_usuario` FOREIGN KEY (`id_usuario`) REFERENCES `usuario` (`id_usuario`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `reserva_facturacion`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `reserva_facturacion` (
  `id_reserva` int NOT NULL,
  `requiere_factura` tinyint(1) DEFAULT NULL,
  `estado_facturacion` enum('sin_definir','no_solicitada','pendiente','registrada','anulada') NOT NULL DEFAULT 'sin_definir',
  `estado_administrativo` enum('pendiente_revision','cerrado') NOT NULL DEFAULT 'pendiente_revision',
  `fecha_decision` datetime DEFAULT NULL,
  `id_usuario_decision` int DEFAULT NULL,
  `fecha_actualizacion` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `id_usuario_actualizacion` int DEFAULT NULL,
  PRIMARY KEY (`id_reserva`),
  KEY `ix_reserva_facturacion_estado` (`estado_facturacion`),
  KEY `ix_reserva_facturacion_fecha` (`fecha_decision`),
  KEY `fk_reserva_facturacion_usuario_decision` (`id_usuario_decision`),
  KEY `fk_reserva_facturacion_usuario_actualizacion` (`id_usuario_actualizacion`),
  CONSTRAINT `fk_reserva_facturacion_reserva` FOREIGN KEY (`id_reserva`) REFERENCES `reserva` (`id_reserva`),
  CONSTRAINT `fk_reserva_facturacion_usuario_actualizacion` FOREIGN KEY (`id_usuario_actualizacion`) REFERENCES `usuario` (`id_usuario`),
  CONSTRAINT `fk_reserva_facturacion_usuario_decision` FOREIGN KEY (`id_usuario_decision`) REFERENCES `usuario` (`id_usuario`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `reserva_facturacion_historial`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `reserva_facturacion_historial` (
  `id_historial` bigint NOT NULL AUTO_INCREMENT,
  `id_reserva` int NOT NULL,
  `accion` varchar(50) NOT NULL,
  `requiere_factura_anterior` tinyint(1) DEFAULT NULL,
  `requiere_factura_nuevo` tinyint(1) DEFAULT NULL,
  `estado_anterior` varchar(30) DEFAULT NULL,
  `estado_nuevo` varchar(30) NOT NULL,
  `detalle` varchar(255) DEFAULT NULL,
  `id_usuario` int NOT NULL,
  `fecha_hora` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id_historial`),
  KEY `ix_facturacion_historial_reserva` (`id_reserva`,`fecha_hora`),
  KEY `ix_facturacion_historial_usuario` (`id_usuario`),
  CONSTRAINT `fk_facturacion_historial_reserva` FOREIGN KEY (`id_reserva`) REFERENCES `reserva` (`id_reserva`),
  CONSTRAINT `fk_facturacion_historial_usuario` FOREIGN KEY (`id_usuario`) REFERENCES `usuario` (`id_usuario`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `reserva_grupo`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `reserva_grupo` (
  `id_reserva_grupo` int NOT NULL AUTO_INCREMENT,
  `id_clipro` int NOT NULL,
  `fecha_creacion` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `observaciones` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id_reserva_grupo`),
  KEY `idx_reserva_grupo_cliente` (`id_clipro`),
  CONSTRAINT `fk_reserva_grupo_cliente` FOREIGN KEY (`id_clipro`) REFERENCES `clipro` (`id_clipro`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `rol`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `rol` (
  `id_rol` int NOT NULL AUTO_INCREMENT,
  `nombre_rol` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `estado` enum('activo','inactivo') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'activo',
  PRIMARY KEY (`id_rol`),
  UNIQUE KEY `uq_rol_nombre` (`nombre_rol`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `rol_permiso`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `rol_permiso` (
  `id_rol` int NOT NULL,
  `id_permiso` int NOT NULL,
  PRIMARY KEY (`id_rol`,`id_permiso`),
  KEY `fk_rol_permiso_permiso` (`id_permiso`),
  CONSTRAINT `fk_rol_permiso_permiso` FOREIGN KEY (`id_permiso`) REFERENCES `permisos` (`id_permiso`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_rol_permiso_rol` FOREIGN KEY (`id_rol`) REFERENCES `rol` (`id_rol`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `subcategoria`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `subcategoria` (
  `id_subcategoria` int NOT NULL AUTO_INCREMENT,
  `id_categoria` int NOT NULL,
  `nombre_subcategoria` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `estado` enum('activo','inactivo') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'activo',
  `precio` decimal(10,2) NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`id_subcategoria`),
  UNIQUE KEY `uq_subcategoria_nombre` (`nombre_subcategoria`),
  KEY `idx_subcategoria_id_categoria` (`id_categoria`),
  KEY `idx_subcategoria_estado` (`estado`),
  CONSTRAINT `fk_subcategoria_categoria` FOREIGN KEY (`id_categoria`) REFERENCES `categoria` (`id_categoria`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `tipo_clipro`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `tipo_clipro` (
  `id_tipoclipro` int NOT NULL AUTO_INCREMENT,
  `tipo` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`id_tipoclipro`),
  UNIQUE KEY `uq_tipo_clipro_tipo` (`tipo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `tipo_estado`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `tipo_estado` (
  `id_tipoestado` int NOT NULL AUTO_INCREMENT,
  `estado` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`id_tipoestado`),
  UNIQUE KEY `uq_tipo_estado_nombre` (`estado`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `tipo_movimiento`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `tipo_movimiento` (
  `id_tipomov` int NOT NULL AUTO_INCREMENT,
  `nombre_tipomov` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`id_tipomov`),
  UNIQUE KEY `uq_tipo_movimiento_nombre` (`nombre_tipomov`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `tipo_proser`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `tipo_proser` (
  `id_tipoproser` int NOT NULL AUTO_INCREMENT,
  `nombre` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`id_tipoproser`),
  UNIQUE KEY `uq_tipo_proser_nombre` (`nombre`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `trabajador`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `trabajador` (
  `id_trabajador` int NOT NULL AUTO_INCREMENT,
  `nombre` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `codigo_servicio` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `telefono` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `observaciones` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `estado` enum('activo','inactivo') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'activo',
  PRIMARY KEY (`id_trabajador`),
  KEY `idx_trabajador_nombre` (`nombre`),
  KEY `idx_trabajador_codigo_servicio` (`codigo_servicio`),
  KEY `idx_trabajador_telefono` (`telefono`),
  KEY `idx_trabajador_estado` (`estado`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `unidad_medida`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `unidad_medida` (
  `id_umedida` int NOT NULL AUTO_INCREMENT,
  `nombre` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`id_umedida`),
  UNIQUE KEY `uq_unidad_medida_nombre` (`nombre`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
DROP TABLE IF EXISTS `usuario`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `usuario` (
  `id_usuario` int NOT NULL AUTO_INCREMENT,
  `id_rol` int NOT NULL,
  `nombre_usuario` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `correo` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `telefono` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `clave` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `fecha_registro` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `estado` enum('activo','inactivo') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'activo',
  `token_recordarme` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `fecha_expiracion_recordarme` datetime DEFAULT NULL,
  `token_recuperacion` varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `fecha_expiracion_recuperacion` datetime DEFAULT NULL,
  PRIMARY KEY (`id_usuario`),
  UNIQUE KEY `uq_usuario_nombre` (`nombre_usuario`),
  UNIQUE KEY `uq_usuario_correo` (`correo`),
  KEY `idx_usuario_id_rol` (`id_rol`),
  KEY `idx_usuario_estado` (`estado`),
  KEY `idx_usuario_token_recordarme` (`token_recordarme`),
  KEY `idx_usuario_token_recuperacion` (`token_recuperacion`),
  CONSTRAINT `fk_usuario_rol` FOREIGN KEY (`id_rol`) REFERENCES `rol` (`id_rol`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

DROP TABLE IF EXISTS `sistema_migracion`;
CREATE TABLE `sistema_migracion` (
  `id_migracion` int NOT NULL AUTO_INCREMENT,
  `nombre` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `checksum` char(64) COLLATE utf8mb4_unicode_ci NOT NULL,
  `fecha_aplicacion` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id_migracion`),
  UNIQUE KEY `uq_sistema_migracion_nombre` (`nombre`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;
