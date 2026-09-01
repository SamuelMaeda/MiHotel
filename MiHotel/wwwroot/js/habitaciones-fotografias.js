document.addEventListener("DOMContentLoaded", function () {
    const controles = document.querySelectorAll(".carga-fotografias");

    controles.forEach(function (control) {
        const input = control.querySelector(".input-fotografias");
        const zona = control.querySelector(".zona-fotografias");
        const mensaje = control.querySelector(".mensaje-fotografias");
        const previsualizaciones = control.querySelector(".previsualizaciones-fotografias");
        const maximoTotal = Number(control.dataset.maximo || 6);
        const existentes = Number(control.dataset.existentes || 0);
        const maximoNuevas = Math.max(0, maximoTotal - existentes);
        const tamanoMaximo = 10 * 1024 * 1024;
        const tiposPermitidos = new Set(["image/jpeg", "image/png", "image/webp"]);
        let archivos = [];
        let urls = [];

        if (!input || !zona || !mensaje || !previsualizaciones) return;

        function mostrarMensaje(texto) {
            mensaje.textContent = texto || "";
            mensaje.style.display = texto ? "block" : "none";
        }

        function actualizarInput() {
            const transferencia = new DataTransfer();
            archivos.forEach(function (archivo) { transferencia.items.add(archivo); });
            input.files = transferencia.files;
        }

        function liberarUrls() {
            urls.forEach(function (url) { URL.revokeObjectURL(url); });
            urls = [];
        }

        function renderizar() {
            liberarUrls();
            previsualizaciones.innerHTML = "";

            archivos.forEach(function (archivo, indice) {
                const contenedor = document.createElement("div");
                contenedor.className = "previsualizacion-fotografia";

                const imagen = document.createElement("img");
                const url = URL.createObjectURL(archivo);
                urls.push(url);
                imagen.src = url;
                imagen.alt = `Fotografía seleccionada ${indice + 1}`;

                const quitar = document.createElement("button");
                quitar.type = "button";
                quitar.className = "quitar-fotografia";
                quitar.title = "Quitar fotografía";
                quitar.setAttribute("aria-label", `Quitar fotografía ${indice + 1}`);
                quitar.innerHTML = '<i class="bi bi-x-lg"></i>';
                quitar.addEventListener("click", function () {
                    archivos.splice(indice, 1);
                    actualizarInput();
                    renderizar();
                    mostrarMensaje("");
                });

                contenedor.append(imagen, quitar);
                previsualizaciones.appendChild(contenedor);
            });
        }

        function claveArchivo(archivo) {
            return `${archivo.name}|${archivo.size}|${archivo.lastModified}`;
        }

        function agregarArchivos(nuevos) {
            mostrarMensaje("");
            const existentesEnSeleccion = new Set(archivos.map(claveArchivo));
            let rechazado = "";

            for (const archivo of nuevos) {
                if (!tiposPermitidos.has(archivo.type)) {
                    rechazado = "Solo se aceptan imágenes JPG, PNG o WEBP.";
                    continue;
                }

                if (archivo.size > tamanoMaximo) {
                    rechazado = `La imagen ${archivo.name} supera el máximo de 10 MB.`;
                    continue;
                }

                if (archivos.length >= maximoNuevas) {
                    rechazado = existentes > 0
                        ? `Solo puede agregar ${maximoNuevas} fotografía(s) más; la habitación ya conserva ${existentes}.`
                        : `Puede seleccionar un máximo de ${maximoTotal} fotografías.`;
                    break;
                }

                const clave = claveArchivo(archivo);
                if (!existentesEnSeleccion.has(clave)) {
                    archivos.push(archivo);
                    existentesEnSeleccion.add(clave);
                }
            }

            actualizarInput();
            renderizar();
            mostrarMensaje(rechazado);
        }

        input.addEventListener("change", function () {
            agregarArchivos(Array.from(input.files || []));
        });

        zona.addEventListener("click", function () { input.click(); });
        zona.addEventListener("keydown", function (evento) {
            if (evento.key === "Enter" || evento.key === " ") {
                evento.preventDefault();
                input.click();
            }
        });

        ["dragenter", "dragover"].forEach(function (nombreEvento) {
            zona.addEventListener(nombreEvento, function (evento) {
                evento.preventDefault();
                zona.classList.add("arrastrando");
            });
        });

        ["dragleave", "drop"].forEach(function (nombreEvento) {
            zona.addEventListener(nombreEvento, function (evento) {
                evento.preventDefault();
                zona.classList.remove("arrastrando");
            });
        });

        zona.addEventListener("drop", function (evento) {
            agregarArchivos(Array.from(evento.dataTransfer?.files || []));
        });

        document.addEventListener("paste", function (evento) {
            const imagenes = Array.from(evento.clipboardData?.items || [])
                .filter(function (item) { return item.kind === "file" && item.type.startsWith("image/"); })
                .map(function (item, indice) {
                    const archivo = item.getAsFile();
                    if (!archivo) return null;
                    const extension = archivo.type === "image/png" ? "png" : archivo.type === "image/webp" ? "webp" : "jpg";
                    return new File([archivo], `habitacion-copiada-${Date.now()}-${indice + 1}.${extension}`, {
                        type: archivo.type,
                        lastModified: Date.now()
                    });
                })
                .filter(Boolean);

            if (imagenes.length > 0) {
                evento.preventDefault();
                agregarArchivos(imagenes);
            }
        });

        window.addEventListener("pagehide", liberarUrls);
    });
});
