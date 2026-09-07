# Programació Docent

Aplicació d'escriptori per al professorat de Catalunya per gestionar l'horari de
classes, prendre notes setmanals per classe i generar informes per assignatura.

- **100 % en català**
- **Portable**: un únic executable per a **Windows 64 bits**, sense instal·lació
  ni permisos d'administrador.
- **Offline**: no fa cap connexió a Internet. Totes les dades es desen localment.
- **Persistent**: base de dades **SQLite** local.
- **Calendari escolar de Catalunya 2026-2027** precarregat i verificat.

---

## 1. Funcionalitats

- **Assistent inicial en dos passos**: en obrir l'aplicació per primera vegada,
  el professor (1) defineix les seves franjes horàries i assignatures i (2)
  **col·loca les classes a la graella per dia** (dilluns a divendres). En afegir
  una franja, l'hora d'inici s'omple **automàticament** amb l'hora de fi de
  l'anterior.
- **Horari editable**: es pot modificar en qualsevol moment des de la secció
  **Configuració** (reducció de jornada, canvi de curs, etc.). L'horari es
  versiona, de manera que canviar-lo **no esborra les notes ja preses** amb
  l'horari anterior.
- **Vista d'horari setmanal** (dilluns–divendres) a la part superior. Es navega
  per setmanes i es marquen els dies festius de Catalunya.
- **Notes per classe i setmana**: en fer clic sobre una classe de la graella,
  s'obre un editor de **text lliure** per anotar el que calgui sobre la classe i
  els alumnes. Les classes amb notes es marquen amb un punt verd.
- **Informes** amb **format elegible** (desplegable): **PDF**, **Excel (XLSX)**
  o **CSV**. Es poden filtrar per **setmana, mes, trimestre o tot el curs** i
  s'ordenen **per assignatura**.
- **Secció Configuració**:
  - **Aparença**: mode **clar / fosc / sistema**, commutable i persistent.
  - **Perfil del professor**: nom, cognoms, centre, departament i correu.
  - **Edició de l'horari**: afegir/eliminar assignatures, franjes i classes.
  - **Còpies de seguretat**: es fa una còpia **automàtica en arrencar**; també
    se'n poden crear manualment i **restaurar-ne** una d'anterior (amb validació
    d'integritat prèvia).
- **Robustesa de dades**: base de dades SQLite amb **WAL**, **migracions
  d'esquema versionades** (per no perdre dades en actualitzar) i **còpies de
  seguretat** rotatives.

---

## 2. Ús a l'equip del professor

1. Copia la carpeta `ProgramacioDocent` (o descomprimeix el ZIP) a l'equip.
2. Executa **`ProgramacioDocent.exe`**.
3. La primera vegada, completa l'assistent d'horari.

### On es desen les dades

Per defecte, a la subcarpeta **`dades/`** al costat de l'executable (mode
portable pur, ideal per a USB). Si aquesta carpeta és de només lectura (unitat
de xarxa, `Program Files`, USB protegit), l'aplicació passa automàticament a:

```
%LOCALAPPDATA%\ProgramacioDocent\
```

La ubicació activa es mostra a la pestanya **Informació** de l'aplicació.

---

## 3. Equips "capats" (restringits)

L'aplicació està pensada per a equips on és difícil instal·lar programari:

- **No cal instal·lador** ni **.NET** preinstal·lat (és autocontinguda).
- **No depèn de WebView2** ni de cap navegador (fa servir el motor gràfic Skia).
- Les **DLL natives** queden al costat de l'executable (no s'extreuen a `%TEMP%`),
  cosa que evita bloquejos habituals d'**AppLocker**.

Consideracions que **no** es poden resoldre només des del codi:

- **SmartScreen**: en ser un `.exe` sense firmar, Windows pot mostrar un avís.
  Cal fer *Més informació → Executa igualment*.
- **Antivirus / GPO**: alguns entorns poden bloquejar executables no firmats.
  En aquest cas, **l'equip d'informàtica del centre** ha d'afegir l'aplicació a
  la llista blanca.
- **Firma de codi (opcional recomanat)**: per eliminar els avisos, es pot signar
  l'executable amb un certificat *code signing* OV (~200-400 €/any).

---

## 4. Com obtenir l'executable (.exe)

Hi ha dues maneres d'obtenir l'aplicació. **La recomanada és la descàrrega des de
Releases**, perquè no requereix instal·lar res al teu equip.

### 4.1. Descàrrega des de Releases (recomanat) ✅

L'executable es compila automàticament a GitHub (GitHub Actions) sobre un equip
Windows i es publica a la pàgina de **Releases** del repositori. Aquesta és la via
ideal, ja que **ni el professor ni tu heu de tenir instal·lat el .NET SDK**.

1. Ves a la pestanya **Releases** del repositori a GitHub.
2. Descarrega `ProgramacioDocent-win-x64.zip` de la darrera versió.
3. Descomprimeix-lo i executa `ProgramacioDocent.exe`.

Per **publicar una versió nova**, crea un tag i puja'l:

```bash
git tag v1.0.0
git push origin v1.0.0
```

El workflow `.github/workflows/release.yml` compilarà el `.exe` i adjuntarà el
ZIP a la Release automàticament. També es pot llançar manualment des de la
pestanya **Actions** (opció *Run workflow*), que genera un artefacte descarregable
sense crear cap Release.

### 4.2. Compilació local (només per a desenvolupadors amb .NET 8 SDK)

> ⚠️ **Important**: el script `publish-portable.ps1` **necessita el .NET 8 SDK
> instal·lat**. Si el fas doble clic no s'executarà (Windows obre els `.ps1` a
> l'editor per seguretat), i si el `dotnet` no hi és, veuràs l'error
> *"La compilació ha fallat"*. Per això, per als equips capats es recomana la via
> 4.1 (Releases), que no depèn de tenir res instal·lat.

Requisits: **.NET 8 SDK**. Des de l'arrel del projecte, en una terminal:

```powershell
pwsh ./publish-portable.ps1
```

Genera:

- `publish/ProgramacioDocent/` — carpeta portable amb `ProgramacioDocent.exe`.
- `publish/ProgramacioDocent-win-x64.zip` — el mateix, empaquetat per distribuir.

Els paràmetres de compilació (tant al script com al workflow) s'han triat a posta:

- `--self-contained true` → no cal .NET a l'equip destí.
- `-p:PublishSingleFile=false` → les DLL natives NO s'extreuen a `%TEMP%`
  (millor compatibilitat amb AppLocker).
- `-p:PublishTrimmed=false` → Avalonia i SQLite usen reflexió; el *trimming*
  podria trencar l'arrencada.

També es pot compilar manualment (fins i tot en cross-compile des de macOS/Linux):

```bash
dotnet publish src/ProgramacioDocent.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false \
  -o publish/ProgramacioDocent
```

---

## 5. Calendari escolar 2026-2027 (Catalunya)

Dades verificades (font: Generalitat de Catalunya i calendari laboral):

| Data | Descripció |
|------|------------|
| 08/09/2026 | Inici de curs |
| 11/09/2026 | Diada Nacional de Catalunya (festiu) |
| 12/10/2026 | Festa Nacional d'Espanya |
| 08/12/2026 | La Immaculada |
| 22/12/2026 – 07/01/2027 | Vacances de Nadal |
| 20/03/2027 – 29/03/2027 | Vacances de Setmana Santa |
| 01/05/2027 | Festa del Treball |
| 21/06/2027 | Fi de curs |

Els **dies de lliure disposició** (fins a 4) els fixa cada centre i es poden
afegir manualment. En centres privats/concertats les dates poden variar
lleugerament respecte del calendari públic.

---

## 6. Protecció de dades (RGPD)

- Les dades **no surten mai de l'equip** (aplicació 100 % local, sense núvol).
- Les notes són de **text lliure**; **es recomana no escriure noms complets
  d'alumnes**. Feu servir inicials o codis per minimitzar dades personals.
- Els **informes exportats** poden contenir dades personals: qui els genera és
  responsable de com els emmagatzema i comparteix.
- Es recomana fer **còpies de seguretat** de la carpeta `dades/` (o del fitxer
  `dades.db`).

---

## 7. Arquitectura

- **UI**: Avalonia UI 11 (MVVM amb CommunityToolkit.Mvvm).
- **Persistència**: SQLite (Microsoft.Data.Sqlite).
- **Informes**: QuestPDF (PDF) i ClosedXML (XLSX); CSV natiu.
- **Idioma**: `CultureInfo("ca-ES")` + tota la interfície en català.

```
ProgramacioDocent/
├── src/
│   ├── Program.cs, App.axaml(.cs), ViewLocator.cs, app.manifest
│   ├── Models/            # Entitats de domini
│   ├── Data/              # Database (SQLite) + CalendariCatalunya (seed)
│   ├── Services/          # Path, Config, Horari, Notes, Calendari, Informe
│   ├── ViewModels/        # MVVM
│   ├── Views/             # MainWindow (horari, notes, informes, informació)
│   └── Assets/            # Icona
├── publish-portable.ps1   # Compilació portable win-x64
└── README.md
```

---

## 8. Full de ruta (Roadmap)

Millores possibles, consensuades amb els perfils d'**arquitectura**,
**desenvolupament fullstack** i **disseny UI/UX**. Cap d'aquestes funcions és
necessària per al funcionament actual; són ampliacions futures. Cada proposta
inclou **prioritat** (Alta/Mitjana/Baixa), **esforç** aproximat i, quan escau,
**risc**.

> **Recomanació transversal dels tres perfils**: abans d'afegir funcions noves,
> cal consolidar dos habilitadors tècnics (migracions d'esquema i còpies de
> seguretat). Sense ells, qualsevol funció futura que canviï la base de dades
> posa en risc les dades ja preses pel professor. Vegeu l'ordre recomanat a
> l'apartat 8.6.

### 8.1. Fonaments de dades (habilitadors)

- **Migracions d'esquema versionades** — *Prioritat Alta · Esforç Mitjà · Risc Mitjà*.
  Sistema `PRAGMA user_version` + scripts SQL incrementals dins de transacció amb
  rollback. **Prerequisit** per afegir taules noves (etiquetes, adjunts,
  recordatoris) sense trencar les dades dels usuaris que actualitzin.
- **Còpies de seguretat i restauració** — *Prioritat Alta · Esforç Baix-Mitjà · Risc Baix*.
  Còpia amb un clic (`VACUUM INTO` / ZIP amb data) + còpia automàtica rotativa en
  obrir/tancar (retenció dels últims N). Restauració amb validació d'integritat
  (`PRAGMA integrity_check`). ROI molt alt en una app local sense núvol.
- **Robustesa de SQLite** — *Prioritat Mitjana · Esforç Baix · Risc Baix*.
  Activar `journal_mode=WAL` per reduir la corrupció davant tancaments bruscos
  (les claus foranes `foreign_keys=ON` ja estan actives).
- **Registre d'errors i diagnòstic** — *Prioritat Mitjana · Esforç Baix*.
  Log rotatiu local (Serilog) i eina que comprovi permisos d'escriptura, ubicació
  de dades i integritat de la BD.
- **Proves automatitzades** — *Prioritat Mitjana · Esforç continu*.
  Tests unitaris dels serveis i tests del model que alimenta els informes; BDs
  "d'or" per validar migracions (SQLite en memòria).

### 8.2. Noves funcionalitats per al professorat

- **Cercador global de notes** — *Prioritat Alta · Esforç Mitjà (3-5 d) · Risc Baix*.
  Amb **SQLite FTS5** (natiu, sense dependències noves) i triggers de
  sincronització. Cerca per text amb operadors (`AND`, `OR`, `"frase"`), resultats
  amb fragment ressaltat i navegació directa a la classe/setmana.
- **Etiquetes/categories a les notes** — *Prioritat Alta · Esforç Baix-Mitjà (2-3 d)*.
  Tags com `#avaluació`, `#reforç`, `#tutoria`, `#incidència` (relació N:M), amb
  xips de color i filtre combinable amb la cerca.
- **Plantilles de notes** — *Prioritat Alta · Esforç Baix (1-2 d)*.
  Plantilles predefinides i pròpies amb marcadors substituïbles (`{{data}}`,
  `{{setmana}}`, `{{assignatura}}`).
- **Copiar setmana → setmana següent** / aplicar plantilla d'horari a un rang —
  *Prioritat Mitjana · Esforç Baix (1-2 d)*. Molt valor amb poc esforç,
  reaprofitant el versionat existent.
- **Gestió de dies de lliure disposició** des de la UI — *Prioritat Mitjana · Esforç Baix*.
  Afegir/eliminar amb calendari visual, a més dels festius precarregats.
- **Recordatoris i tasques pendents** — *Prioritat Mitjana · Esforç Mitjà (4-6 d)*.
  Vinculats a classe/data. **Sense** notificacions del sistema (poc fiables en
  equips capats); en el seu lloc, un **panell "Pendents"** a l'inici amb els
  venciments propers marcats per proximitat.
- **Adjuntar fitxers a una nota** — *Prioritat Mitjana · Esforç Mitjà (3-4 d)*.
  Els binaris **no** es desen dins SQLite (inflaria la BD i penalitzaria els
  backups): es copien a `attachments/{guid}.{ext}` amb **rutes relatives** per
  mantenir la portabilitat, i només es desen la ruta i les metadades.
- **Suport de diversos cursos escolars** i canvi ràpid entre ells —
  *Prioritat Mitjana · Esforç Mitjà*.
- **Importació/exportació de la configuració d'horari** (JSON) i **importació de
  calendari `.ics`** — *Prioritat Mitjana · Esforç Baix-Mitjà*. Per compartir
  plantilles entre professors i carregar festius d'altres cursos.

### 8.3. Informes

- **Personalització de l'informe** — *Prioritat Alta · Esforç Mitjà (3-5 d)*.
  Portada amb logotip del centre, nom del professor i curs; capçalera/peu amb
  numeració de pàgina i data de generació.
- **Rang de dates personalitzat** — *Prioritat Alta · Esforç Baix*.
  A més dels filtres actuals (setmana/mes/trimestre/curs), selecció de-a amb
  `DatePicker`.
- **Informe global multi-assignatura** — *Prioritat Mitjana · Esforç Baix-Mitjà*.
  Visió de conjunt del trimestre, a més de l'informe per assignatura actual.
- **XLSX millorat** — *Prioritat Mitjana · Esforç Baix-Mitjà*.
  Fulls per assignatura, taules amb format (files alternes), congelar capçaleres,
  filtres automàtics i colors per etiqueta.
- **Inclusió de tags i filtres per etiqueta** als informes — *Prioritat Mitjana*.
- **Previsualització de l'informe** dins l'aplicació abans d'exportar-lo —
  *Prioritat Baixa · Esforç Mitjà*.

### 8.4. Experiència d'usuari i disseny (UI/UX)

- **Sistema de tokens de color (`DynamicResource`)** — *Prioritat Alta · Esforç Mitjà · Risc Baix*.
  Substituir els colors literals actuals per tokens. **Cal fer-ho aviat**: és
  l'habilitador del mode fosc i evita un refactor dolorós més endavant.
- **Retroalimentació visual d'accions** — *Prioritat Alta · Esforç Mitjà · Risc Baix*.
  Confirmació de desat ("Desat ✓"), avisos no bloquejants (toasts) en exportar,
  indicador de progrés en generar informes i diàlegs d'error clars en català.
- **Mode fosc + selector clar/fosc/sistema** — *Prioritat Alta · Esforç Mitjà · Risc Baix*.
  Persistir la preferència i aplicar-la a l'arrencada per evitar el "flaix" blanc.
- **Accessibilitat** — *Prioritat Mitjana · Esforç Mitjà · Risc Baix*.
  Contrast AA, mida de lletra configurable (molt valorada pel professorat sènior),
  navegació completa per teclat (`TabIndex`, `AutomationProperties`) i festius
  indicats també amb icona (no només color).
- **Onboarding progressiu i estats buits útils** — *Prioritat Mitjana · Esforç Mitjà*.
  Assistent per passos amb barra de progrés i missatges d'estat buit amb acció
  ("Encara no tens notes aquesta setmana. Fes clic a una classe per començar").
- **Vista de dia i vista de mes** — *Prioritat Mitjana · Esforç Alt · Risc Mitjà*.
  A més de la vista de setmana. La vista de mes connecta horari i calendari
  escolar; cal virtualització per no penalitzar equips antics.
- **Indicadors visuals a la graella** — *Prioritat Mitjana · Esforç Baix*.
  Ressaltar el dia actual i marcar visualment els dies festius i les hores lliures.
- **Panell/dashboard "Avui"** a l'inici — *Prioritat Mitjana · Esforç Mitjà*.
  Classes del dia, propers festius, últimes notes editades i accés ràpid a informes.
- **Identitat visual definitiva** — *Prioritat Mitjana · Esforç Baix-Mitjà*.
  Icona i logotip propis (substituir la icona provisional) i **fonts incrustades
  al binari** per garantir consistència en equips capats sense fonts modernes.
- **Personalització visual de l'horari** — *Prioritat Baixa · Esforç Baix-Mitjà*.
  Colors per assignatura amb paleta accessible predefinida i densitat de graella
  (compacta/còmoda).
- **Micro-interaccions** — *Prioritat Baixa · Esforç Baix · Risc Mitjà*.
  Transicions suaus (<200 ms) amb **opció manual de reduir animacions** per a
  maquinari feble.
- **Localització addicional** — *Prioritat Baixa · Esforç Baix*.
  Mantenint el català com a objectiu, deixar preparada l'estructura per a altres
  llengües si calgués.

### 8.5. Robustesa, distribució i actualitzacions

- **Signatura de codi (certificat OV)** — *Prioritat Alta · Esforç Baix tècnic + cost/tràmit · Risc Alt si NO es fa*.
  Elimina els avisos de SmartScreen i el bloqueig d'antivirus, que són el
  principal fre d'adopció en els equips capats objectiu. **No es pot resoldre des
  del codi.**
- **Instal·lació per-usuari sense administrador** — *Prioritat Alta · Esforç Mitjà · Risc Mitjà*.
  Opció d'instal·lació a `%LOCALAPPDATA%`/MSIX, amb les dades sempre a
  `%APPDATA%`/`%LOCALAPPDATA%` i mai al costat de l'`.exe`.
- **Xifratge opcional de la base de dades** — *Prioritat Mitjana · Esforç Mitjà*.
  Amb contrasenya (AES-GCM gestionat en .NET, sense DLL natives addicionals) per a
  equips compartits físicament.
- **Actualitzador integrat offline-friendly** — *Prioritat Mitjana · Esforç Mitjà*.
  Comprovació no bloquejant + baixada manual, o Velopack amb actualitzacions
  delta; cada versió aplica les migracions pendents.
- **Separació de capes** (Domain/Application/Infrastructure/UI) —
  *Prioritat Mitjana · Esforç Mitjà-Alt · Risc Mitjà*.
  Introduir interfícies de repositori de manera incremental (amb tests de
  caracterització) perquè es pugui substituir `SqliteRepository` per
  `ApiRepository` sense tocar la UI. És l'habilitador de la modalitat
  servidor-client (8.7).

### 8.6. Ordre d'execució recomanat

1. **Fonaments de dades**: WAL → migracions versionades → còpies de seguretat.
2. **Base visual** (abans d'afegir UI nova): tokens de color → retroalimentació
   visual → mode fosc.
3. **Valor docent**: cercador FTS5 → etiquetes → plantilles → copiar setmana.
4. **Informes** (portada, rang de dates, multi-assignatura, XLSX) + accessibilitat.
5. **Distribució**: signatura de codi + instal·lació per-usuari.
6. **Llarg termini**: separació de capes → modalitat servidor-client (8.7).

> **Criteri d'enginyeria**: en una app local d'un sol usuari, **perdre dades o no
> poder-se instal·lar** són els riscos que "maten" el producte. Per això es
> prioritzen la integritat/recuperació de dades i l'eliminació de friccions de
> distribució **abans** de refactors arquitectònics grans.

### 8.7. Cap a una modalitat servidor–client

Si en el futur es vol centralitzar les dades (diversos professors, accés des de
diversos dispositius, còpies centralitzades), aquesta seria la línia d'evolució.
L'arquitectura actual ja separa la **lògica de serveis** de la **UI**, cosa que
facilita el canvi de font de dades.

- **Backend / API**: exposar els serveis actuals (`HorariService`, `NotesService`,
  `CalendariService`, `InformeService`) mitjançant una **API REST** (ASP.NET Core
  Minimal API o Web API), reaprofitant els mateixos models de domini.
- **Base de dades centralitzada**: migrar de SQLite local a **PostgreSQL** o
  **SQL Server**, introduint una capa de repositoris/ORM (Entity Framework Core)
  per abstreure el proveïdor de dades.
- **Autenticació i autorització**: login per professor (OAuth2/OpenID Connect o
  integració amb el directori del centre, p. ex. Google Workspace o Microsoft
  Entra ID) i rols (professor, cap d'estudis, administrador).
- **Client**: reaprofitar la mateixa aplicació Avalonia com a client d'escriptori
  que consumeix l'API, i/o afegir un **client web** (Blazor) per accedir des del
  navegador sense instal·lar res.
- **Sincronització i mode offline**: cua local de canvis i sincronització quan hi
  hagi connexió, per no perdre la robustesa del funcionament sense Internet.
- **Panell d'administració del centre**: gestió d'usuaris, informes agregats per
  departament o per curs, i exportacions massives.
- **Desplegament**: contenidors **Docker** per al backend i la base de dades,
  desplegables en un servidor del centre o al núvol.
- **Còpies de seguretat i alta disponibilitat** centralitzades, gestionades pel
  servidor en lloc de per cada equip.
- **Compliment RGPD reforçat**: en centralitzar dades de menors, caldrà revisar
  les mesures de seguretat, els accessos, la retenció de dades i el consentiment,
  amb l'assessorament del responsable de protecció de dades del centre.

> **Nota**: aquest pas canvia el model actual (100 % local i sense servidor) i
> implica requisits d'infraestructura, manteniment i seguretat més exigents. És
> recomanable només si la necessitat de compartir dades entre diversos usuaris ho
> justifica.
