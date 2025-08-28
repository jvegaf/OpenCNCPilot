# Makefile para OpenCNCPilot (.NET 8 + Avalonia)
# Uso básico: `make help`

# Variables
SLN ?= OpenCNCPilot.Modern.sln
UI_PROJECT ?= src/OpenCNCPilot.UI/OpenCNCPilot.UI.csproj
CONFIG ?= Release
RID ?= linux-x64
RIDS ?= linux-x64 win-x64
PUBLISH_DIR ?= publish/$(RID)
DOTNET ?= dotnet

# Phony targets
.PHONY: help restore build test coverage format clean run run-ui watch watch-nohot publish publish-all package ci doctor chatmodes-sync

help:
	@echo "Targets disponibles:"
	@echo "  restore       - Restaura paquetes NuGet"
	@echo "  build         - Compila la solución ($(SLN)) en $(CONFIG)"
	@echo "  test          - Ejecuta tests con logger TRX"
	@echo "  coverage      - Ejecuta tests y colecta cobertura (XPlat Code Coverage)"
	@echo "  format        - Aplica dotnet format"
	@echo "  clean         - Limpia build y carpeta publish"
	@echo "  run           - Ejecuta la aplicación (alias de run-ui)"
	@echo "  run-ui        - Ejecuta el proyecto UI en $(CONFIG)"
	@echo "  watch         - Inicia dotnet watch run del UI (Hot Reload)"
	@echo "  watch-nohot   - Inicia dotnet watch sin Hot Reload (fallback)"
	@echo "  publish       - Publica UI self-contained single-file para $(RID)"
	@echo "  publish-all   - Publica UI para todos los RIDs: $(RIDS)"
	@echo "  package       - Empaqueta artefacto de $(RID) (zip/tar.gz)"
	@echo "  ci            - Formatea, compila y testea (pipeline local)"
	@echo "  doctor        - Diagnóstico de SDK/runtimes .NET y ASP.NET Core (para watch)"
	@echo "  chatmodes-sync - Descarga/actualiza chatmodes desde awesome-copilot"
	@echo "Variables: SLN, UI_PROJECT, CONFIG, RID, RIDS"

restore:
	$(DOTNET) restore $(SLN)

build: restore
	$(DOTNET) build -c $(CONFIG) $(SLN) --nologo

test:
	$(DOTNET) test -c $(CONFIG) $(SLN) --nologo --logger trx

coverage:
	$(DOTNET) test -c $(CONFIG) $(SLN) --nologo --collect:"XPlat Code Coverage"

format:
	$(DOTNET) format --no-restore

clean:
	$(DOTNET) clean $(SLN)
	rm -rf publish

run-ui:
	$(DOTNET) run -c $(CONFIG) --project $(UI_PROJECT)

run: run-ui

watch:
	$(DOTNET) watch run -c $(CONFIG) --project $(UI_PROJECT)

watch-nohot:
	$(DOTNET) watch --no-hot-reload run -c $(CONFIG) --project $(UI_PROJECT)

publish: build
	$(DOTNET) publish $(UI_PROJECT) -c $(CONFIG) -r $(RID) \
		--self-contained true -p:PublishSingleFile=true \
		-p:IncludeNativeLibrariesForSelfExtract=true -o $(PUBLISH_DIR)

publish-all:
	@set -e; \
	for r in $(RIDS); do \
		echo "Publishing $$r"; \
		$(DOTNET) publish $(UI_PROJECT) -c $(CONFIG) -r $$r \
			--self-contained true -p:PublishSingleFile=true \
			-p:IncludeNativeLibrariesForSelfExtract=true -o publish/$$r; \
	done

package:
	@set -e; \
	if [[ "$(RID)" == win* ]]; then \
		cd publish/$(RID) && zip -rq ../../OpenCNCPilot-$(RID).zip .; \
	else \
		tar -C publish/$(RID) -czf OpenCNCPilot-$(RID).tar.gz .; \
	fi

ci: format build test

chatmodes-sync: ## Descarga/actualiza chatmodes desde awesome-copilot
	sh ./scripts/sync-chatmodes.sh

doctor:
	@set -e; \
	echo "==> Diagnóstico de entorno .NET"; \
	if ! command -v $(DOTNET) >/dev/null 2>&1; then \
		echo "[ERROR] 'dotnet' no encontrado en PATH. Instala .NET SDK 8.0."; \
		echo "Guía: https://learn.microsoft.com/dotnet/core/install/"; \
		exit 1; \
	fi; \
	echo "-- dotnet --info --"; $(DOTNET) --info | sed -n '1,20p' || true; \
	echo "-- SDKs instalados --"; $(DOTNET) --list-sdks || true; \
	echo "-- Runtimes instalados --"; $(DOTNET) --list-runtimes || true; \
	if ! $(DOTNET) --list-runtimes | grep -E '^Microsoft\\.AspNetCore\\.App\\s+8\\.' >/dev/null; then \
		echo "[WARN] Falta el runtime 'Microsoft.AspNetCore.App 8.x' (requerido por dotnet watch Hot Reload)."; \
		if [ -f /etc/os-release ]; then \
			. /etc/os-release; \
			case "$$ID" in \
				arch|manjaro|endeavouros) echo "Sugerencia (Arch): sudo pacman -S aspnet-runtime dotnet-runtime";; \
				ubuntu|debian) echo "Sugerencia (Debian/Ubuntu): sudo apt-get update && sudo apt-get install aspnetcore-runtime-8.0";; \
				fedora) echo "Sugerencia (Fedora): sudo dnf install aspnetcore-runtime-8.0";; \
				opensuse*|sles) echo "Sugerencia (openSUSE/SLES): sudo zypper install aspnetcore-runtime-8.0";; \
				*) echo "Consulta tu distro: https://learn.microsoft.com/dotnet/core/install/linux";; \
			esac; \
		else \
			echo "Consulta instalación Linux: https://learn.microsoft.com/dotnet/core/install/linux"; \
		fi; \
		echo "Alternativa inmediata: 'make watch-nohot' para ver cambios sin Hot Reload."; \
	else \
		echo "[OK] Runtime ASP.NET Core 8.x encontrado. 'make watch' debería funcionar."; \
	fi
