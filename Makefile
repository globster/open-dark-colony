########################
# OpenRA Dark Colony Mod
########################

include mod.config

# Determine engine directory
ENGINEDIR = engine

.PHONY: all engine check-engine check-sdk-scripts clean

all: check-engine
	@command -v $(ENGINEDIR)/fetch-engine.sh > /dev/null 2>&1 || (echo "OpenRA engine not found. Run 'make engine' first."; exit 1)
	@cd $(ENGINEDIR) && make all

engine:
	@echo ">>> Fetching OpenRA engine $(ENGINE_VERSION)..."
	@mkdir -p $(ENGINEDIR)
	@if [ ! -f $(ENGINEDIR)/Makefile ]; then \
		curl -sL $(AUTOMATIC_ENGINE_SOURCE) -o /tmp/$(AUTOMATIC_ENGINE_TEMP_ARCHIVE_NAME) && \
		unzip -qo /tmp/$(AUTOMATIC_ENGINE_TEMP_ARCHIVE_NAME) -d /tmp && \
		cp -r /tmp/$(AUTOMATIC_ENGINE_EXTRACT_DIRECTORY)/* $(ENGINEDIR)/ && \
		rm -rf /tmp/$(AUTOMATIC_ENGINE_TEMP_ARCHIVE_NAME) /tmp/$(AUTOMATIC_ENGINE_EXTRACT_DIRECTORY); \
	fi
	@echo ">>> Engine ready."

check-engine:
	@if [ ! -f $(ENGINEDIR)/Makefile ]; then \
		echo "Engine not found. Run 'make engine' to download it."; \
		exit 1; \
	fi

clean:
	@if [ -f $(ENGINEDIR)/Makefile ]; then \
		cd $(ENGINEDIR) && make clean; \
	fi
	@echo "Cleaned."

launch:
	@$(ENGINEDIR)/launch-game.sh Game.Mod=dc
