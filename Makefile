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
.PHONY: help restore build test coverage format clean run run-ui publish publish-all package ci chatmodes-sync

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
	@echo "  publish       - Publica UI self-contained single-file para $(RID)"
	@echo "  publish-all   - Publica UI para todos los RIDs: $(RIDS)"
	@echo "  package       - Empaqueta artefacto de $(RID) (zip/tar.gz)"
	@echo "  ci            - Formatea, compila y testea (pipeline local)"
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
