CLUSTER := invoiceflow
CONTEXT := k3d-$(CLUSTER)

# Set here as well as in .envrc, so make is safe even without direnv
export DOCKER_HOST := unix://$(HOME)/.colima/invoiceflow/docker.sock
export KUBECONFIG := $(CURDIR)/.kube/config
export INVOICEFLOW_BACKUP_DIR ?= $(HOME)/.local/share/invoiceflow/backups

KUBECTL := kubectl --context $(CONTEXT)

.DEFAULT_GOAL := help
.PHONY: help doctor cluster-up cluster-down cluster-status ollama-up ollama-down

help:
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk -F':.*## ' '{printf "  %-16s %s\n", $$1, $$2}'

doctor: ## Check that Docker and kubectl target the local environment only
	@./scripts/doctor.sh

cluster-up: doctor ## Create the k3d cluster (no-op if it exists)
	@mkdir -p "$(INVOICEFLOW_BACKUP_DIR)" "$(dir $(KUBECONFIG))"
	@if k3d cluster list $(CLUSTER) >/dev/null 2>&1; then \
		echo "Cluster $(CLUSTER) already exists"; \
	else \
		k3d cluster create --config deploy/k3d/cluster.yaml; \
	fi
	@./scripts/doctor.sh
	@$(KUBECTL) wait --for=condition=Ready nodes --all --timeout=120s
	@$(KUBECTL) get nodes

cluster-down: ## Delete the k3d cluster and its registry
	@k3d cluster delete $(CLUSTER)
	@k3d registry delete invoiceflow-registry 2>/dev/null || true

cluster-status: doctor ## Show nodes and system pods
	@$(KUBECTL) get nodes -o wide
	@$(KUBECTL) get pods -n kube-system

ollama-up: ## Start Ollama on the host, listening on localhost only
	@if curl -sf http://127.0.0.1:11434/api/version >/dev/null; then \
		echo "Ollama is already running"; \
	else \
		mkdir -p "$(HOME)/Library/Logs"; \
		OLLAMA_HOST=127.0.0.1:11434 nohup ollama serve >"$(HOME)/Library/Logs/ollama.log" 2>&1 & \
		sleep 2; curl -sf http://127.0.0.1:11434/api/version && echo; \
	fi

ollama-down: ## Stop Ollama
	@pkill -f "ollama serve" && echo "Ollama stopped" || echo "Ollama was not running"
