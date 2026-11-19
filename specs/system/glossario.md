# Glossário

| Termo | Significado |
|---|---|
| **Host** | quem hospeda a partida; é o servidor Mirror e decide tudo |
| **Cliente** | quem entra na partida do host |
| **Olá / Hello** | primeira mensagem do cliente com o mod; depois dela o host pode mandar mensagens do mod |
| **Panela** | `CookingPot-01` do jogo (um `Pan`); a frigideira (`Pan`) não conta |
| **Ingrediente** | item cru que vira sopa na panela (hoje: tomate, batata) |
| **Porção** | uma refeição de sopa; cada ingrediente rende `PorcoesPorIngrediente` |
| **Evaporar** | sopa pronta esquecida no fogo aceso perde porções |
| **Recipiente** | onde a sopa é servida: prato (do mod) ou caneca (`Mug` do jogo ou de outro mod) |
| **Caneca colorida** | caneca de outro mod (Nomad Items) que é a `Mug` do jogo com outro material |
| **Molde (template)** | clone inativo de um prefab do jogo que vira item novo do mod |
| **assetId** | id do Mirror que diz qual prefab criar nos clientes |
| **Chave de save** | `CozinhaExtra:…`, gravada no lugar do GUID do prefab |
| **Painel de ações** | lista "Ação [tecla]" do jogo perto da mira; o mod acrescenta linhas (`ActionHint`) |
| **Aviso (toast)** | mensagem curta na tela com a resposta do host (`CeFeedback`) |
| **Kit de teste** | F8 no host cria os itens para testar; desligado por padrão |
| **Stripping** | o jogo vem com DLLs .NET/Unity enxugadas; o RefCheck confere |
