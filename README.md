# Job Radar Local

Application web locale V0.2 pour importer des entreprises, des offres et un CV, calculer des scores de compatibilite, visualiser les entreprises sur une carte Leaflet et generer un rapport Markdown.

## Prerequis

- .NET SDK 8 ou plus recent
- Node.js 20 ou plus recent
- npm

## Lancement

Terminal 1 :

```bash
cd backend
dotnet run
```

Terminal 2 :

```bash
cd frontend
npm install
npm run dev
```

Ouvrir ensuite :

```txt
http://127.0.0.1:5173
```

L'API ecoute par defaut sur :

```txt
http://127.0.0.1:5087
```

## Carte

La page `/map` propose deux fonds :

- `OpenStreetMap` : fond lisible par defaut, necessite un acces reseau pour charger les tuiles.
- `Local schematique` : fallback sans appel externe.

Les donnees entreprises, offres, scores et rapports restent stockes localement.

## Parcours de test

1. Aller dans `/companies` et importer `data/samples/companies.sample.csv`.
2. Aller dans `/jobs` et importer `data/samples/jobs.sample.csv`.
3. Aller dans `/profile` et importer `data/samples/cv.sample.txt`.
4. Recalculer les scores depuis `/dashboard` ou `/jobs`.
5. Aller dans `/map` pour voir les entreprises et filtrer les resultats.
6. Aller dans `/report` et generer un rapport Markdown.

Les rapports sont ecrits dans :

```txt
data/reports/
```

La base SQLite locale est creee automatiquement dans :

```txt
data/job-radar-local.db
```

## Formats CSV

### Entreprises

Colonnes attendues :

```csv
name,domain,secondary_domains,city,address,latitude,longitude,website,career_url,linkedin_url,glassdoor_url,known_stack,notes,logo_url
```

Champs obligatoires : `name`, `domain`, `city`, `latitude`, `longitude`.

Listes separees par `;` : `secondary_domains`, `known_stack`.

Une entreprise deja presente avec le meme nom et la meme ville est mise a jour.

### Offres

Colonnes attendues :

```csv
company_name,title,location,remote_policy,contract,salary_min,salary_max,seniority,job_type,stack,description,url,publication_date
```

Champs obligatoires : `company_name`, `title`.

Liste separee par `;` : `stack`.

Les doublons sont evites avec la combinaison `company_name + title + url`.

## CV

Formats supportes en V0.2 :

- `.txt`
- `.md`

PDF et DOCX ne sont pas parses dans cette version. Le parsing est isole derriere `ICvParsingService` pour permettre un ajout ulterieur sans modifier les endpoints.

Les fichiers samples sont fictifs et ne doivent pas contenir de donnees personnelles.

## Tests

Depuis la racine du projet :

```bash
dotnet test
```

## Structure

```txt
backend/
frontend/
data/
  samples/
    companies.sample.csv
    jobs.sample.csv
    cv.sample.txt
  reports/
README.md
```
