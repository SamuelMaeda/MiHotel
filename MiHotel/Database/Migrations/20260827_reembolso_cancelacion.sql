-- Permite registrar la devolución de pagos cuando se cancela una estadía
-- pagada y el importe ya no puede trasladarse dentro de la reserva agrupada.
INSERT INTO tipo_movimiento (nombre_tipomov)
SELECT 'reembolso'
WHERE NOT EXISTS (
    SELECT 1
    FROM tipo_movimiento
    WHERE LOWER(nombre_tipomov) = 'reembolso'
);
