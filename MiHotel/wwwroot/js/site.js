// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener("DOMContentLoaded", function () {
    // Las explicaciones emergentes se reservan para acciones sin texto visible.
    const selectorAyudaIcono = ".mh-icon-button[title], .btn-icono[title], .btn-close[title], .ampliar-factura[title]";
    document.querySelectorAll("a[title], button[title]").forEach(function (elemento) {
        if (!elemento.matches(selectorAyudaIcono)) {
            elemento.removeAttribute("title");
        }
    });

    if (window.bootstrap?.Tooltip) {
        document.querySelectorAll(selectorAyudaIcono).forEach(function (elemento) {
            if (!bootstrap.Tooltip.getInstance(elemento)) {
                new bootstrap.Tooltip(elemento, { trigger: "hover focus", container: "body" });
            }
        });
    }

    // Ordenamiento ligero para tablas completas que no utilizan paginación del servidor.
    document.querySelectorAll("table[data-mh-sortable]").forEach(function (tabla) {
        const encabezados = tabla.querySelectorAll("thead th:not([data-no-sort])");

        encabezados.forEach(function (encabezado) {
            encabezado.classList.add("mh-sortable-heading");
            encabezado.tabIndex = 0;
            encabezado.setAttribute("role", "button");
            encabezado.setAttribute("aria-label", `Ordenar por ${encabezado.textContent.trim()}`);
            encabezado.insertAdjacentHTML("beforeend", '<i class="bi bi-arrow-down-up mh-sort-icon" aria-hidden="true"></i>');

            function ordenar() {
                const filaEncabezado = Array.from(encabezado.parentElement.children);
                const indice = filaEncabezado.indexOf(encabezado);
                const cuerpo = tabla.tBodies[0];
                if (!cuerpo) return;

                const filas = Array.from(cuerpo.rows).filter(fila => fila.cells.length > 1 && !fila.querySelector("[colspan]"));
                const ascendente = encabezado.dataset.direction !== "asc";

                filas.sort(function (a, b) {
                    return compararValores(a.cells[indice]?.textContent ?? "", b.cells[indice]?.textContent ?? "", ascendente);
                });

                encabezados.forEach(th => {
                    delete th.dataset.direction;
                    th.setAttribute("aria-sort", "none");
                    const icono = th.querySelector(".mh-sort-icon");
                    if (icono) icono.className = "bi bi-arrow-down-up mh-sort-icon";
                });

                encabezado.dataset.direction = ascendente ? "asc" : "desc";
                encabezado.setAttribute("aria-sort", ascendente ? "ascending" : "descending");
                const icono = encabezado.querySelector(".mh-sort-icon");
                if (icono) icono.className = `bi ${ascendente ? "bi-sort-up" : "bi-sort-down"} mh-sort-icon`;
                filas.forEach(fila => cuerpo.appendChild(fila));
            }

            encabezado.addEventListener("click", ordenar);
            encabezado.addEventListener("keydown", evento => {
                if (evento.key === "Enter" || evento.key === " ") {
                    evento.preventDefault();
                    ordenar();
                }
            });
        });
    });
});

function compararValores(valorA, valorB, ascendente) {
    const normalizar = valor => valor.replace(/\s+/g, " ").trim();
    const a = normalizar(valorA);
    const b = normalizar(valorB);
    const fechaRegex = /^(\d{2})\/(\d{2})\/(\d{4})(?:\s+(\d{2}):(\d{2}))?/;
    const fechaA = a.match(fechaRegex);
    const fechaB = b.match(fechaRegex);
    let resultado;

    if (fechaA && fechaB) {
        const convertirFecha = partes => new Date(Number(partes[3]), Number(partes[2]) - 1, Number(partes[1]), Number(partes[4] || 0), Number(partes[5] || 0)).getTime();
        resultado = convertirFecha(fechaA) - convertirFecha(fechaB);
    } else {
        const esNumero = valor => /^(?:Q\s*)?-?[\d,.]+$/.test(valor);
        if (esNumero(a) && esNumero(b)) {
            const numeroA = Number(a.replace(/[^0-9.-]/g, ""));
            const numeroB = Number(b.replace(/[^0-9.-]/g, ""));
            resultado = numeroA - numeroB;
        } else {
            resultado = a.localeCompare(b, "es", { sensitivity: "base", numeric: true });
        }
    }

    return ascendente ? resultado : -resultado;
}
