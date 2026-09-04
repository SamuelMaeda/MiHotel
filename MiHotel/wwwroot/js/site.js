// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener("DOMContentLoaded", function () {
    estandarizarVistaActual();
    inicializarAutocompletados(document);

    // Las explicaciones emergentes se reservan para acciones sin texto visible.
    const selectorAyudaIcono = ".mh-icon-button[title], .btn-icono[title], .btn-close[title], .ampliar-factura[title], .mh-status-indicator[title]";
    const botonesConAyuda = "button[title], a.mh-btn[title], a.mh-icon-button[title], a.btn[title], a[class*='btn-'][title]";
    document.querySelectorAll(botonesConAyuda).forEach(function (elemento) {
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
            if (!encabezado.querySelector(".mh-sort-icon")) {
                encabezado.insertAdjacentHTML("beforeend", '<i class="bi bi-arrow-down-up mh-sort-icon" aria-hidden="true"></i>');
            }

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

    const observadorAutocompletados = new MutationObserver(function (mutaciones) {
        mutaciones.forEach(function (mutacion) {
            mutacion.addedNodes.forEach(function (nodo) {
                if (nodo.nodeType === Node.ELEMENT_NODE) {
                    inicializarAutocompletados(nodo);
                }
            });
            mutacion.removedNodes.forEach(function (nodo) {
                if (nodo.nodeType !== Node.ELEMENT_NODE) return;
                const selectoresEliminados = [];
                if (nodo.matches?.("select[data-mh-autocomplete]")) selectoresEliminados.push(nodo);
                nodo.querySelectorAll?.("select[data-mh-autocomplete]").forEach(selector => selectoresEliminados.push(selector));
                selectoresEliminados.forEach(selector => selector._mhAutocompleteLista?.remove());
            });
        });
    });
    observadorAutocompletados.observe(document.body, { childList: true, subtree: true });
});

let consecutivoAutocomplete = 0;

function inicializarAutocompletados(raiz) {
    const selectores = [];
    if (raiz.matches?.("select[data-mh-autocomplete]")) selectores.push(raiz);
    raiz.querySelectorAll?.("select[data-mh-autocomplete]").forEach(selector => selectores.push(selector));
    selectores.forEach(inicializarAutocomplete);
}

function inicializarAutocomplete(selector) {
    if (selector.dataset.mhAutocompleteInicializado === "true") return;
    selector.dataset.mhAutocompleteInicializado = "true";

    const opciones = Array.from(selector.options)
        .filter(opcion => opcion.value && !opcion.disabled)
        .map(opcion => ({
            value: opcion.value,
            texto: opcion.textContent.trim(),
            busqueda: normalizarTextoAutocomplete(opcion.textContent)
        }));
    const requerido = selector.required || selector.hasAttribute("data-val-required");
    const identificador = selector.id || `mhAutocomplete${++consecutivoAutocomplete}`;
    const textoVacio = selector.dataset.mhEmptyText || "Sin selección";
    const placeholder = selector.dataset.mhSearchPlaceholder || "Escriba para buscar...";

    const contenedor = document.createElement("div");
    contenedor.className = "mh-autocomplete";

    const entrada = document.createElement("input");
    entrada.type = "text";
    entrada.id = `${identificador}Busqueda`;
    entrada.className = "form-control mh-autocomplete-input";
    entrada.placeholder = placeholder;
    entrada.autocomplete = "off";
    entrada.required = requerido;
    entrada.setAttribute("role", "combobox");
    entrada.setAttribute("aria-autocomplete", "list");
    entrada.setAttribute("aria-expanded", "false");

    const icono = document.createElement("i");
    icono.className = "bi bi-search mh-autocomplete-icon";
    icono.setAttribute("aria-hidden", "true");

    const lista = document.createElement("div");
    lista.id = `${identificador}Sugerencias`;
    lista.className = "mh-autocomplete-list";
    lista.setAttribute("role", "listbox");
    lista.hidden = true;
    entrada.setAttribute("aria-controls", lista.id);

    selector.parentNode.insertBefore(contenedor, selector);
    contenedor.appendChild(entrada);
    contenedor.appendChild(icono);
    document.body.appendChild(lista);
    selector._mhAutocompleteLista = lista;
    selector.classList.add("mh-autocomplete-source");
    selector.required = false;
    selector.tabIndex = -1;
    selector.setAttribute("aria-hidden", "true");

    if (selector.id) {
        document.querySelectorAll(`label[for="${selector.id}"]`).forEach(etiqueta => {
            etiqueta.setAttribute("for", entrada.id);
        });
    }

    let indiceActivo = -1;
    let resultadosActuales = [];

    function opcionSeleccionada() {
        return selector.selectedOptions[0]?.value ? selector.selectedOptions[0] : null;
    }

    function sincronizarEntrada() {
        const opcion = opcionSeleccionada();
        entrada.value = opcion?.textContent.trim() || "";
        entrada.dataset.valorSeleccionado = opcion?.value || "";
        entrada.setCustomValidity("");
    }

    function posicionarLista() {
        const rectangulo = entrada.getBoundingClientRect();
        lista.style.left = `${rectangulo.left}px`;
        lista.style.top = `${rectangulo.bottom + 4}px`;
        lista.style.width = `${rectangulo.width}px`;
    }

    function cerrarLista() {
        lista.hidden = true;
        entrada.setAttribute("aria-expanded", "false");
        indiceActivo = -1;
    }

    function marcarActivo(indice) {
        const elementos = lista.querySelectorAll(".mh-autocomplete-option");
        elementos.forEach(elemento => elemento.classList.remove("activo"));
        indiceActivo = indice;
        const activo = elementos[indiceActivo];
        if (activo) {
            activo.classList.add("activo");
            activo.scrollIntoView({ block: "nearest" });
        }
    }

    function seleccionar(opcion) {
        selector.value = opcion.value;
        entrada.value = opcion.texto;
        entrada.dataset.valorSeleccionado = opcion.value;
        entrada.setCustomValidity("");
        selector.dispatchEvent(new Event("change", { bubbles: true }));
        cerrarLista();
    }

    function mostrarResultados() {
        const termino = normalizarTextoAutocomplete(entrada.value);
        resultadosActuales = opciones
            .filter(opcion => !termino || opcion.busqueda.includes(termino))
            .slice(0, 10);
        lista.replaceChildren();
        indiceActivo = -1;

        if (resultadosActuales.length === 0) {
            const vacio = document.createElement("div");
            vacio.className = "mh-autocomplete-empty";
            vacio.textContent = "No se encontraron coincidencias.";
            lista.appendChild(vacio);
        } else {
            resultadosActuales.forEach(function (opcion, indice) {
                const boton = document.createElement("button");
                boton.type = "button";
                boton.className = "mh-autocomplete-option";
                boton.setAttribute("role", "option");
                boton.textContent = opcion.texto;
                boton.addEventListener("mousedown", evento => evento.preventDefault());
                boton.addEventListener("click", () => seleccionar(opcion));
                boton.addEventListener("mousemove", () => marcarActivo(indice));
                lista.appendChild(boton);
            });
        }

        posicionarLista();
        lista.hidden = false;
        entrada.setAttribute("aria-expanded", "true");
    }

    function validarSeleccion() {
        const tieneTexto = Boolean(entrada.value.trim());
        const esValido = Boolean(selector.value) || (!requerido && !tieneTexto);
        entrada.setCustomValidity(esValido
            ? ""
            : tieneTexto
                ? "Seleccione una opción de la lista de coincidencias."
                : `Seleccione una opción. ${textoVacio}`);
        return esValido;
    }

    entrada.addEventListener("focus", mostrarResultados);
    entrada.addEventListener("input", function () {
        const seleccionAnterior = selector.value;
        const opcionActual = opcionSeleccionada();
        if (!opcionActual || normalizarTextoAutocomplete(entrada.value) !== normalizarTextoAutocomplete(opcionActual.textContent)) {
            selector.value = "";
            entrada.dataset.valorSeleccionado = "";
            if (seleccionAnterior) selector.dispatchEvent(new Event("change", { bubbles: true }));
        }
        entrada.setCustomValidity("");
        mostrarResultados();
    });
    entrada.addEventListener("keydown", function (evento) {
        if (evento.key === "ArrowDown") {
            evento.preventDefault();
            if (lista.hidden) mostrarResultados();
            marcarActivo(Math.min(indiceActivo + 1, resultadosActuales.length - 1));
        } else if (evento.key === "ArrowUp") {
            evento.preventDefault();
            marcarActivo(Math.max(indiceActivo - 1, 0));
        } else if (evento.key === "Enter" && indiceActivo >= 0) {
            evento.preventDefault();
            seleccionar(resultadosActuales[indiceActivo]);
        } else if (evento.key === "Escape") {
            cerrarLista();
        }
    });
    entrada.addEventListener("blur", function () {
        validarSeleccion();
        window.setTimeout(cerrarLista, 120);
    });
    selector.addEventListener("change", sincronizarEntrada);
    selector.form?.addEventListener("submit", function (evento) {
        if (!selector.isConnected) return;
        if (!validarSeleccion()) {
            evento.preventDefault();
            entrada.reportValidity();
        }
    });
    window.addEventListener("resize", () => !lista.hidden && posicionarLista());
    window.addEventListener("scroll", () => !lista.hidden && posicionarLista(), true);
    document.addEventListener("mousedown", function (evento) {
        if (!contenedor.contains(evento.target) && !lista.contains(evento.target)) cerrarLista();
    });

    sincronizarEntrada();
}

function normalizarTextoAutocomplete(texto) {
    return (texto || "")
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .toLocaleLowerCase("es")
        .trim();
}

function estandarizarVistaActual() {
    const principal = document.querySelector("main");
    if (!principal) return;

    const ruta = window.location.pathname.toLowerCase();
    if (ruta.includes("/acceso/") || principal.querySelector(".contenedor-login")) {
        principal.classList.add("mh-standard-login");
        asegurarIconos(principal);
        return;
    }

    const shell = encontrarContenedorPrincipal(principal);
    if (!shell) return;

    shell.classList.add("mh-page-shell", "mh-auto-shell");

    const tieneFormulario = Boolean(shell.querySelector("form"));
    const tieneListado = Boolean(shell.querySelector("table, .tabs, .tabs-vista, .paginacion, .paginas"));
    const esCapturaCompleja = Boolean(shell.querySelector("#tablaVentas, #tablaProductos, .tabla-pos, .tabla-permisos"));
    const esRutaFormulario = /\/(crear|editar)(?:\/|$)/.test(ruta);
    if (tieneFormulario && !esCapturaCompleja && (!tieneListado || esRutaFormulario)) {
        shell.classList.add("mh-page-shell--form");
    }

    estandarizarEncabezado(shell);
    estandarizarBusquedas(shell);
    estandarizarTablas(shell);
    estandarizarBotones(shell);
    estandarizarAccionesInferiores(shell);
}

function estandarizarBusquedas(shell) {
    shell.querySelectorAll("form").forEach(function (formulario) {
        const campo = formulario.querySelector("input[name='busqueda']:not([type='hidden'])");
        if (!campo || formulario.classList.contains("mh-search-form")) return;

        formulario.classList.add("mh-search-form");
        campo.classList.remove("form-control-custom");
        campo.classList.add("form-control", "mh-search-input");

        let boton = formulario.querySelector("button[type='submit'], button:not([type])");
        if (!boton) {
            boton = document.createElement("button");
            boton.type = "submit";
            boton.innerHTML = '<i class="bi bi-search" aria-hidden="true"></i> Buscar';
        }
        boton.classList.add("mh-btn", "mh-btn-neutral", "mh-search-button");

        const contenedorCampo = obtenerHijoDirecto(formulario, campo);
        const contenedorBoton = obtenerHijoDirecto(formulario, boton);
        const fila = document.createElement("div");
        fila.className = "mh-search-row";
        formulario.insertBefore(fila, contenedorCampo);

        const etiqueta = formulario.querySelector("label");
        if (etiqueta) {
            etiqueta.classList.add("form-label", "mh-search-label");
            if (etiqueta.parentElement !== formulario) formulario.insertBefore(etiqueta, fila);
        }

        fila.appendChild(campo);
        fila.appendChild(boton);

        [contenedorCampo, contenedorBoton].forEach(function (contenedor) {
            if (contenedor && contenedor !== campo && contenedor !== boton &&
                contenedor !== fila && contenedor.children.length === 0 && !contenedor.textContent.trim()) {
                contenedor.remove();
            }
        });

    });
}

function obtenerHijoDirecto(contenedor, elemento) {
    let actual = elemento;
    while (actual.parentElement && actual.parentElement !== contenedor) {
        actual = actual.parentElement;
    }
    return actual;
}

function encontrarContenedorPrincipal(principal) {
    const selectores = [
        ".mh-page-shell",
        ".tarjeta-contenido",
        ".tarjeta",
        ".contenedor-facturas",
        ".pos-container",
        ".reporte",
        ".panel-contenido",
        ".tarjeta-detalle",
        ".tarjeta-confirmacion",
        ".simulador",
        ".simulacion-contenido",
        ".container-fluid",
        ".container"
    ];

    for (const selector of selectores) {
        const candidato = principal.querySelector(selector);
        if (candidato) return candidato;
    }

    return principal;
}

function estandarizarEncabezado(shell) {
    if (shell.classList.contains("panel-contenido")) return;

    let encabezado = shell.querySelector(":scope > .mh-page-header, :scope > .encabezado, :scope > .header-formulario, :scope > .encabezado-principal, :scope > .reporte-cabecera, :scope > .simulacion-encabezado");
    if (!encabezado) {
        const primerBloque = shell.firstElementChild;
        const esCabeceraCompuesta = primerBloque?.matches("div") &&
            Boolean(primerBloque.querySelector("h1, h2, h3, h4")) &&
            !primerBloque.querySelector("form, table");
        if (esCabeceraCompuesta) encabezado = primerBloque;
    }
    if (encabezado) {
        encabezado.classList.add("mh-page-header--auto");
        return;
    }

    const titulo = Array.from(shell.children).find(elemento => /^H[1-4]$/.test(elemento.tagName));
    if (!titulo) return;

    encabezado = document.createElement("div");
    encabezado.className = "mh-page-header mh-page-header--auto";
    shell.insertBefore(encabezado, titulo);
    encabezado.appendChild(titulo);

    const siguiente = encabezado.nextElementSibling;
    if (siguiente?.matches("p.text-muted, p.descripcion-pagina, .subtitulo")) {
        const bloqueTexto = document.createElement("div");
        encabezado.insertBefore(bloqueTexto, titulo);
        bloqueTexto.appendChild(titulo);
        bloqueTexto.appendChild(siguiente);
    }
}

function estandarizarTablas(shell) {
    const tablasNoOrdenables = "#tablaVentas, #tablaProductos, .tabla-pos, .tabla-permisos, [data-mh-no-sort]";

    shell.querySelectorAll("table").forEach(function (tabla) {
        tabla.classList.add("mh-table");

        const encabezados = tabla.querySelectorAll("thead th");
        if (encabezados.length === 0) return;

        const ultimoEncabezado = encabezados[encabezados.length - 1];
        if (/acciones|acción|opciones/i.test(ultimoEncabezado.textContent.trim()) || ultimoEncabezado.textContent.trim() === "") {
            ultimoEncabezado.dataset.noSort = "true";
        }

        const yaOrdenaEnServidor = Boolean(tabla.querySelector("thead .encabezado-orden"));
        const contieneCaptura = Boolean(tabla.querySelector("tbody input:not([type='hidden']), tbody select, tbody textarea"));
        const contieneFilasEspeciales = Boolean(tabla.querySelector("tbody tr > [colspan]"));
        if (!tabla.matches(tablasNoOrdenables) && !yaOrdenaEnServidor && !contieneCaptura && !contieneFilasEspeciales && encabezados.length > 1) {
            tabla.dataset.mhSortable = "true";
        }

        if (!tabla.parentElement?.matches(".table-responsive, .tabla-scroll") && !tabla.matches(tablasNoOrdenables)) {
            const envoltorio = document.createElement("div");
            envoltorio.className = "table-responsive";
            tabla.parentNode.insertBefore(envoltorio, tabla);
            envoltorio.appendChild(tabla);
        }
    });
}

function estandarizarBotones(shell) {
    const controles = shell.querySelectorAll("a, button, input[type='submit']");

    controles.forEach(function (control) {
        if (control.matches(".tab, .tab-vista, .pagina, .pagina-link, .encabezado-orden, .nav-link, .btn-close, .abrir-dpi, .abrir-vista-previa-dpi, .ampliar-factura")) return;

        const texto = obtenerTextoControl(control);
        const esAccionBreveDeTabla = Boolean(control.closest("td")) &&
            control.classList.contains("btn-eliminar") && texto.length <= 2;
        const esIcono = control.classList.contains("btn-icono") ||
            control.classList.contains("mh-icon-button") ||
            esAccionBreveDeTabla ||
            (control.matches("a, button") && !texto && Boolean(control.querySelector("i, svg")));

        if (esIcono) {
            control.classList.add("mh-icon-button");
            asignarTipoIcono(control, texto);
            if (esAccionBreveDeTabla && !control.querySelector("i, svg")) {
                control.innerHTML = '<i class="bi bi-trash-fill" aria-hidden="true"></i>';
            }
            asegurarAyudaIcono(control);
            return;
        }

        const tieneClaseBoton = Array.from(control.classList).some(clase =>
            clase === "btn" || clase.startsWith("btn-"));
        const pareceBoton = control.matches("button, input[type='submit']") || tieneClaseBoton;
        if (!pareceBoton) return;

        control.classList.add("mh-btn");
        asignarTipoBoton(control, texto);
    });

    asegurarIconos(shell);

    const encabezado = shell.querySelector(":scope > .mh-page-header, :scope > .mh-page-header--auto");
    const accionCrud = Array.from(shell.querySelectorAll("a.mh-btn-primary")).find(control =>
        !control.closest("form") && /nuevo|nueva|crear/.test(obtenerTextoControl(control).toLowerCase()));
    if (encabezado && accionCrud && !encabezado.contains(accionCrud)) {
        encabezado.appendChild(accionCrud);
    }
}

function obtenerTextoControl(control) {
    if (control.tagName === "INPUT") return (control.value || "").trim();
    const copia = control.cloneNode(true);
    copia.querySelectorAll("i, svg, .spinner-border").forEach(icono => icono.remove());
    return copia.textContent.replace(/\s+/g, " ").trim();
}

function asignarTipoBoton(control, texto) {
    const clave = `${texto} ${control.className} ${control.getAttribute("href") || ""} ${control.getAttribute("formaction") || ""}`.toLowerCase();
    control.classList.remove("mh-btn-primary", "mh-btn-cancel", "mh-btn-back", "mh-btn-neutral");

    if (/volver|regresar|retornar/.test(clave)) {
        control.classList.add("mh-btn-back");
    } else if (/cancelar|anular|eliminar|inactivar|editar|corregir/.test(clave)) {
        control.classList.add("mh-btn-cancel");
    } else if (/buscar|filtrar|consultar|ver historial/.test(clave)) {
        control.classList.add("mh-btn-neutral");
    } else {
        control.classList.add("mh-btn-primary");
    }
}

function asignarTipoIcono(control, texto) {
    const clave = `${texto} ${control.className} ${control.getAttribute("title") || ""} ${control.getAttribute("aria-label") || ""} ${control.getAttribute("href") || ""}`.toLowerCase();
    control.classList.remove("mh-icon-view", "mh-icon-edit", "mh-icon-cancel", "mh-icon-positive");

    if (/ver|detalle|consultar|visualizar|eye/.test(clave)) {
        control.classList.add("mh-icon-view");
    } else if (/editar|corregir|pencil/.test(clave)) {
        control.classList.add("mh-icon-edit");
    } else if (/cancelar|eliminar|inactivar|anular|trash|x-circle/.test(clave)) {
        control.classList.add("mh-icon-cancel");
    } else {
        control.classList.add("mh-icon-positive");
    }
}

function asegurarIconos(shell) {
    shell.querySelectorAll(".mh-btn").forEach(function (control) {
        if (control.tagName === "INPUT" || control.querySelector("i, svg")) return;

        const texto = obtenerTextoControl(control).toLowerCase();
        let icono = "bi-check-circle-fill";
        if (/nuevo|nueva|agregar|crear/.test(texto)) icono = "bi-plus-circle-fill";
        else if (/guardar|actualizar/.test(texto)) icono = "bi-floppy-fill";
        else if (/volver|regresar|retornar/.test(texto)) icono = "bi-arrow-left-circle-fill";
        else if (/cancelar|anular|inactivar/.test(texto)) icono = "bi-x-circle-fill";
        else if (/eliminar/.test(texto)) icono = "bi-trash-fill";
        else if (/editar|corregir/.test(texto)) icono = "bi-pencil-fill";
        else if (/abrir|ver|detalle|expediente/.test(texto)) icono = "bi-eye-fill";
        else if (/buscar|consultar/.test(texto)) icono = "bi-search";
        else if (/mes actual|calendario/.test(texto)) icono = "bi-calendar-month-fill";
        else if (/calcular|simular/.test(texto)) icono = "bi-calculator-fill";
        else if (/pagar|abono|cobrar/.test(texto)) icono = "bi-cash-coin";
        else if (/completar|finalizar/.test(texto)) icono = "bi-check-circle-fill";

        control.insertAdjacentHTML("afterbegin", `<i class="bi ${icono}" aria-hidden="true"></i>`);
    });
}

function asegurarAyudaIcono(control) {
    let ayuda = control.getAttribute("title") || control.getAttribute("aria-label");
    if (!ayuda) {
        const clave = `${control.className} ${control.getAttribute("href") || ""}`.toLowerCase();
        if (/detalle|view|eye/.test(clave)) ayuda = "Ver detalle";
        else if (/editar|edit|pencil/.test(clave)) ayuda = "Editar registro";
        else if (/cancelar|inactivar|eliminar|trash/.test(clave)) ayuda = "Cancelar o inactivar";
        else ayuda = "Realizar acción";
    }
    control.setAttribute("title", ayuda);
    control.setAttribute("aria-label", ayuda);
}

function estandarizarAccionesInferiores(shell) {
    const regresos = Array.from(shell.querySelectorAll(".mh-btn-back"));
    regresos.forEach(function (regreso) {
        const padre = regreso.parentElement;
        if (!padre) return;

        // Las acciones de retorno no compiten con la acción principal del encabezado.
        // Se trasladan al pie sin alterar su URL, formulario ni comportamiento.
        if (padre.matches(".mh-page-header, .mh-page-header--auto")) {
            let barraInferior = shell.querySelector(":scope > .mh-global-bottom-actions");
            if (!barraInferior) {
                barraInferior = document.createElement("div");
                barraInferior.className = "mh-global-bottom-actions mh-bottom-actions mh-action-group";
                shell.appendChild(barraInferior);
            }
            barraInferior.appendChild(regreso);
            return;
        }

        const accionesHermanas = padre.querySelectorAll(":scope > a, :scope > button, :scope > form");
        if (accionesHermanas.length > 0) {
            padre.classList.add("mh-action-group");
            if (padre.matches(".acciones, .botones, .acciones-formulario") || padre === shell.lastElementChild) {
                padre.classList.add("mh-bottom-actions");
            }
        }
    });
}

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
