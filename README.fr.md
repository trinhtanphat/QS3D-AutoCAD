# QS3D AutoCAD

**Langues :** [English](README.md) · [Tiếng Việt](README.vi.md) · [简体中文](README.zh-CN.md) · [한국어](README.ko.md) · [Русский](README.ru.md) · [Français](README.fr.md)

QS3D AutoCAD est l’hôte Autodesk AutoCAD de QS3D pour les workflows de modélisation structurelle et de quantités.

## Générations AutoCAD prises en charge

- AutoCAD 2021–2024 : un unique payload hôte legacy .NET Framework 4.8 compilé avec `AutoCAD.NET` 24.0.0 appartenant à Autodesk et chargé depuis la série de bundles `R24.0-R24.3`
- AutoCAD 2025 : payload hôte .NET 8 compilé avec `AutoCAD.NET` 25.0.1 appartenant à Autodesk
- AutoCAD 2026 : même famille de payload ciblant .NET 8 ; la native acceptance enregistre le CLR réellement observé sur l’hôte, car AutoCAD 2026.1.2+ fait évoluer l’hôte vers .NET 10
- AutoCAD 2027 : payload hôte .NET 10 compilé avec `AutoCAD.NET` 26.0.0 appartenant à Autodesk
- le code spécifique à Autodesk est isolé du QS3D Core indépendant de l’hôte
- le déploiement utilise un `.bundle` AutoCAD
- le pipeline de release produit à la fois un bundle zip portable et un installateur autonome `QS3D-AutoCAD-<version>-Setup.exe`

Le payload legacy est construit une seule fois avec le managed SDK AutoCAD 2021 puis réutilisé pour AutoCAD 2021, 2022, 2023 et 2024. La matrice de compatibilité managed d’Autodesk prend explicitement en charge les anciens SDK R24.x sur les hôtes R24.x plus récents ; QS3D n’a donc pas besoin de dupliquer quatre binaires net48 équivalents. Le payload legacy est construit et empaqueté indépendamment des payloads modernes .NET 8/.NET 10 ; l’ajout de la détection 2022–2024 ne rétrograde pas les binaires 2025–2027.

AutoCAD 2026 impose une frontière native-runtime supplémentaire : le payload QS3D 2026 livré reste ciblé sur .NET 8, tandis qu’un véritable hôte AutoCAD 2026 peut signaler un CLR major 8 ou 10 selon son niveau de mise à jour. Le CI hébergé valide uniquement la compatibilité source/packaging. Il ne remplace jamais le chargement du candidate exact dans une installation AutoCAD 2026 réelle et l’enregistrement des CLR/native checks effectivement observés.

## Workflow de modélisation implémenté

Exécutez `QS3D` pour lazy-load le plugin et ouvrir l’espace de travail QS3D ancrable. L’hôte actuel implémente :

- `QS3DINIT` — initialiser/renommer le projet QS3D stocké dans le DWG
- `QS3DLEVEL` — marqueur de Level
- `QS3DGRID` — axe de Grid
- `QS3DCOLUMN` — poteau structurel 3D
- `QS3DBEAM` — poutre 3D orientée selon le plan
- `QS3DSLAB` — dalle 3D rectangulaire
- `QS3DWALL` — mur 3D orienté selon le plan
- `QS3DCURTAIN` — panneaux curtain modulaires
- `QS3DSECTION` — marqueur de section
- `QS3DBOQ` — synthèse des quantités à partir des entities marquées QS3D
- `QS3DEDIT` — modifier les propriétés QS3D et reconstruire les solids physiques lorsque les dimensions changent
- `QS3DASSIGNLEVEL` — lier un élément structurel à un QS3D Level et le déplacer/reconstruire à cette élévation
- `QS3DLEVELMOVE` — modifier l’élévation d’un Level et propager le déplacement Z à tous les éléments structurels liés à ce Level
- `QS3DBINDGRID` — attacher une ou deux références Grid sémantiques à un élément structurel
- `QS3DGRIDSNAP` — reconstruire ensemble la géométrie et les metadata d’un élément structurel lié à un Grid
- `QS3DREFERENCERENAME` — renommer les références Level/Grid tout en conservant les semantic IDs et les bindings
- `QS3DLEVELSEQUENCE` — ordonner les noms de Level par élévation
- `QS3DGRIDSEQUENCE` — réordonner une famille de Grid parallèles selon leur ordre spatial
- `QS3DCLEARREFS` — supprimer les références de placement Level/Grid sans déplacer la géométrie
- `QS3DGRIDARRAY` — créer une série nommée de Grid parallèles à espacement fixe
- `QS3DREFERENCEDELETE` — supprimer un Level/Grid inutilisé tout en refusant l’opération si des dépendances subsistent
- `QS3DREFERENCES` — lister les références Level/Grid et le nombre de dépendances
- `QS3DCOLUMNJIG`, `QS3DBEAMJIG`, `QS3DSLABJIG`, `QS3DWALLJIG`, `QS3DCURTAINJIG` — previews temporaires live-solid pendant la création, avec retour sur dimensions/orientation et persistance uniquement au commit
- `QS3DRIBBON` — réconcilier/créer le Ribbon QS3D via les runtime UI types `Autodesk.Windows` déjà chargés par AutoCAD
- `QS3DREFRESH` — actualiser le model browser
- `QS3DABOUT` — informations host/runtime

L’espace de travail ancrable possède les onglets Tools, Project et Levels & Grids. Le project browser liste les entities appartenant à QS3D, se synchronise avec la sélection pickfirst d’AutoCAD, expose les propriétés de géométrie, de quantité et de placement-reference, et peut lancer une édition sûre. Les contrôles de palette peuvent basculer entre le vietnamien et l’anglais.

La géométrie générée contient des QS3D XData typées. L’identité/le nom du projet est stocké dans le DWG Named Objects Dictionary, de sorte que l’état QS3D voyage avec le dessin. Les metadata actuelles utilisent le schema rétrocompatible `QS3D2` pour les références Level/Grid tout en continuant à lire les entities legacy `QS3D1`. Les changements de propriétés d’un solid ou de placement par Level conservent les semantic IDs QS3D tout en remplaçant ou déplaçant la géométrie physique, ce qui empêche les metadata BOQ de diverger du modèle visible.

L’implémentation JIG/Grid-manager est complète au niveau source mais reste soumise à la native acceptance sur un hôte réel. Un build hébergé ne prouve pas que les cursor previews, l’affichage du Ribbon, undo/redo ou la persistance se comportent correctement dans chaque génération AutoCAD prise en charge.

### Frontière Ribbon

Le Ribbon bridge ne compile volontairement **pas** directement avec `AdWindows.dll` ni `Autodesk.Windows`. Le CI hébergé ne peut ni remplacer ni simuler cette dépendance UI native d’AutoCAD. `QS3DRIBBON` résout au runtime les assembly/types UI AutoCAD déjà chargés, construit un onglet QS3D idempotent avec des panneaux Model/References/Review et échoue de manière non bloquante afin que les commandes palette/model restent utilisables si l’API Ribbon n’est pas disponible.

Une compilation hébergée réussie prouve seulement que le source du bridge reste sûr pour l’hôte. `ribbon_surface` et `ribbon_visual_qa` restent des native acceptance gates ; la matrice legacy AutoCAD 2021–2024 peut être qualifiée séparément, tandis que la matrice de qualification production par défaut reste AutoCAD 2025, 2026 et 2027 jusqu’à modification intentionnelle de la release policy.

## Build et livraison

Le `CI` GitHub build et smoke-test le Core indépendant de l’hôte, compile l’unique payload legacy net48 AutoCAD 2021–2024, le payload net8 AutoCAD 2025–2026 et le payload net10 AutoCAD 2027 avec des packages appartenant à Autodesk, valide l’architecture command/bundle, empaquette un engineering release candidate et vérifie de bout en bout la release provenance et les checksums. Les assemblies Autodesk ne sont que des dépendances de compilation et sont exclues des payloads de release QS3D.

Le CI valide également le native-acceptance tooling lui-même et démontre qu’une evidence synthétique contenant des checks `pending` est rejetée à la fois pour la matrice moderne par défaut et pour la matrice legacy AutoCAD 2021–2024 séparée. Le CI hébergé ne produit jamais de native PASS.

`./scripts/package.ps1 -Version <version>` crée :

- `artifacts/QS3D-AutoCAD-<version>.zip`
- `artifacts/QS3D-AutoCAD-<version>-Setup.exe`
- `artifacts/RELEASE-PROVENANCE.json`
- `artifacts/SHA256SUMS.txt`

`RELEASE-PROVENANCE.json` enregistre le source commit exact, la version, les trois familles de runtime payload, l’état de signature, les tailles des artifacts et les hash SHA-256. `./scripts/verify-artifacts.ps1 -Version <version>` vérifie indépendamment ce contract.

L’exécutable Setup embarque le bundle et l’installe dans le répertoire Autodesk `ApplicationPlugins` commun à tous les utilisateurs. Install/upgrade est effectué par staging avec rollback sûr ; Setup refuse install, upgrade ou `--uninstall` tant qu’AutoCAD est en cours d’exécution.

La publication d’un tag fonctionne en fail-closed : le tagged SHA doit se trouver sur `main`, correspondre exactement à la repository variable `QS3D_NATIVE_ACCEPTED_SHA`, et de vrais secrets Authenticode PFX/password doivent être configurés. Le workflow signe les plugin assemblies et Setup.exe, vérifie ces signatures/provenance, puis seulement crée une GitHub prerelease. Le packaging manuel reste adapté à l’engineering validation mais ne doit pas être présenté comme une production release signée lorsque la provenance indique `signed=false`.

Le plugin actuel n’envoie aucune telemetry et n’effectue aucun production licensing call. Consultez `docs/PRIVACY.md` pour la posture de confidentialité actuelle et `docs/RELEASE-SECURITY.md` pour les release/signing gates.

Un source build vert n’est pas une native runtime qualification. Le bundle exact généré doit encore subir une acceptance testing dans un véritable AutoCAD. La matrice formelle de release par défaut reste AutoCAD 2025/2026/2027. AutoCAD 2021/2022/2023/2024 utilisent une matrice legacy evidence séparée, et chaque host testé doit disposer d’une evidence sur hôte réel avant de pouvoir être qualifié native-qualified. L’evidence AutoCAD 2026 doit aussi enregistrer le CLR concret observé après le chargement de QS3D afin de ne pas masquer la transition du host AutoCAD 2026.1.2+ vers .NET 10. Consultez `docs/NATIVE-ACCEPTANCE.md` pour le workflow evidence exact.

Consultez `docs/IMPLEMENTATION-PLAN.md` et `docs/BUILD.md` pour l’architecture, le build et les native acceptance gates.
