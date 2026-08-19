# Restaurante Inteligente — MVP v2

Versão evoluída do projeto enviado.

## Já implementado
- Login JWT
- Usuário administrador inicial
- Multi-tenant por `restaurantId`
- Produtos: listar, criar, editar e excluir logicamente
- Ingredientes/estoque: listar, criar, atualizar quantidade e críticos
- Mesas: listar, criar e alterar status
- Pedidos: criar, listar e fechar com entrada no caixa
- Dashboard com faturamento, pedidos, ticket médio, estoque crítico e pedidos abertos
- Assistente de IA com consultas reais para faturamento e estoque
- Base preparada para Azure OpenAI
- PostgreSQL + Redis via Docker
- Frontend Next.js responsivo

## Login demo
E-mail: admin@demo.local
Senha: Admin@123

## Rodar
```powershell
docker compose up -d
cd backend/Restaurante.Api
dotnet restore
dotnet run
```
Em outro terminal:
```powershell
cd frontend
npm install
copy .env.local.example .env.local
npm run dev
```
Frontend: http://localhost:3000
API/Swagger: http://localhost:5000/swagger

> O backend usa `EnsureCreated()` para facilitar o MVP local. Antes de produção, migrar para EF Core Migrations.
