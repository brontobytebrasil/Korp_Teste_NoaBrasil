-- Um banco por serviço. Se os dois lessem a mesma tabela, a separação em
-- microsserviços seria só de fachada: qualquer mudança de schema de um lado
-- quebraria o outro, e o acoplamento voltaria pela porta dos fundos.
CREATE DATABASE estoque;
CREATE DATABASE faturamento;
