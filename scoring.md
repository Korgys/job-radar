# Systeme de notation

Les scores servent a prioriser les entreprises et les offres a traiter en premier. Ils sont recalcules depuis `/dashboard` ou `/jobs` apres import des entreprises, des offres et du CV.

## Entreprises

Score total : 100 points.

| Rubrique | Maximum | Regle |
| --- | ---: | --- |
| Technique | 70 | Correspondance entre les competences detectees dans le CV et la stack connue de l'entreprise, completee par la stack des offres liees. |
| Domaine | 30 | Alignement entre les domaines detectes dans le CV et le domaine principal ou secondaire de l'entreprise. |

Detail domaine :

- domaine identique : 30 points ;
- domaine proche : 21 points ;
- domaine non aligne ou non renseigne : 0 point.

## Offres

Score total : 100 points.

| Rubrique | Maximum | Regle |
| --- | ---: | --- |
| Technique | 40 | Correspondance entre la stack technique de l'offre et les competences detectees dans le CV. |
| Experience | 30 | Compatibilite entre le niveau d'experience du CV et le niveau attendu par l'offre. |
| Role | 20 | Alignement entre le role detecte dans le CV et le poste propose. |
| Domaine | 10 | Alignement avec un secteur deja detecte dans le CV. |

Detail experience :

- profil compatible avec le niveau attendu : 30 points ;
- profil un niveau sous le niveau attendu : 20 points ;
- profil deux niveaux sous le niveau attendu : 10 points ;
- profil trop eloigne : 0 point ;
- si l'offre ne donne pas de niveau, l'application infere le niveau depuis l'intitule ou la description ; sans indice, elle suppose un niveau confirme, environ 3-4 ans d'experience.

Detail role :

- role aligne : 20 points ;
- role adjacent backend / fullstack ou frontend / fullstack : 10 points ;
- role non aligne : 0 point.

Exemples :

- developpeur backend vers offre backend : 20 points ;
- developpeur backend vers offre fullstack : 10 points ;
- developpeur frontend vers offre devops : 0 point.
