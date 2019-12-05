# DAD

Grupo 7
Gabriel Figueira Nº86426
Miguel Trinca Nº86490
Pedro Carvalho Nº86499


Para executar o projeto basta:
 - Abrir a solution no Visual Studio;
 - Ir às properties da Solution->Startup Project->Mutiple Startup Project e selecionar Start para o PuppetMaster e o PCS;
 - Fazer Build e depois Start.


NOTAS:
- O comando de adicionar Rooms pressupõe que o servidor já existe e que a Location dessa Room já está presente no sistema.
- Locations e Rooms podem ser pré-configuradas num ficheiro dentro da pasta ServerConfig no Server que tem o seguinte formato:
    location_name:n_rooms,room1,capacity,room2,capacity,...,roomN,capacity