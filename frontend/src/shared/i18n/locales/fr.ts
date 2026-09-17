// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   fr — French.
//
// Usage:
//   Loaded by shared/i18n/index.tsx and selected by the language
//   picker. Never imported by a screen — a screen calls t(key), and
//   which catalogue answers is not its business.
//
// Coding Instructions:
//   Dealership vocabulary, not literal translation. A `lead` is a *demande*
//   and a `deal` is a *vente*; "piste" and "affaire" are marketing and
//   banking words that a French vehicle salesperson does not use for these.
//   Accounting terms follow the plan comptable: *balance générale*, *grand
//   livre*, *exercice*.
//
//   Typography is French typography: a narrow no-break space ( )
//   before ? ! : and ; — a plain space lets the punctuation wrap onto the
//   next line on its own, which looks like a rendering fault. Quotation
//   marks are « ». The apostrophe is ’ and not '.

import type { Catalogue } from '../index';

export const fr: Catalogue = {
  'app.name': 'DealerFOSS',
  'common.loading': 'Chargement…',
  'common.save': 'Enregistrer',
  'common.saving': 'Enregistrement…',
  'common.cancel': 'Annuler',
  'common.close': 'Fermer',
  'confirm.typeToConfirm': 'Saisissez {text} pour confirmer.',
  'common.retry': 'Réessayer',
  'common.search': 'Rechercher',
  'common.searching': 'Recherche…',
  'common.none': 'Aucun',
  'common.all': 'Tous',
  'common.yes': 'Oui',
  'common.no': 'Non',
  'common.back': 'Retour',
  'common.continue': 'Continuer',
  'common.unexpected': 'Une erreur s’est produite. Réessayez.',
  'common.notPermitted': 'Vous n’avez pas l’autorisation de voir ceci.',
  'common.unreachable': 'Impossible de joindre le serveur. Est-il démarré ?',

  'record.opening': 'Ouverture de la fiche…',
  'record.unreachable':
    'Cette fiche ne peut pas être ouverte. Elle a peut-être été supprimée, ou elle relève d’une partie de l’entreprise que vous n’êtes pas autorisé à voir.',
  'record.backToList': 'Retour à la liste',

  'error.network': 'Impossible de joindre le serveur. Est-il démarré ?',
  'error.invalidCredentials':
    'Cette adresse e-mail et ce mot de passe ne correspondent à aucun compte.',
  'error.sessionRequired': 'Connectez-vous pour utiliser cette fonction.',
  'error.adminSessionRequired':
    'Connectez-vous en tant qu’administrateur pour accéder à la console. Une connexion de concession n’y donne pas accès.',
  'error.sessionInvalid': 'Votre session a pris fin. Reconnectez-vous.',
  'error.antiForgeryFailed':
    'Ce navigateur ne détient plus de jeton correspondant à sa session. Reconnectez-vous.',
  'error.secondFactorRejected': 'Ce code n’a pas été accepté.',
  'error.secondFactorRequired':
    'Votre rôle exige la connexion en deux étapes. Configurez-la pour accéder au reste de l’application.',
  'error.mfaNotEnrolled': 'La connexion en deux étapes n’est pas configurée sur ce compte.',
  'error.mfaAlreadyOn': 'La connexion en deux étapes est déjà activée sur ce compte.',
  'error.notATenantCaller':
    'Un administrateur ne peut pas agir en tant qu’utilisateur d’une concession.',
  'error.tenantRequired': 'Indiquez de quel groupe de concessions il s’agit.',
  'error.tenantNotFound': 'Aucun groupe de concessions actif ne porte ce nom.',

  'shell.skipToContent': 'Aller au contenu',
  'shell.mainNavigation': 'Principal',
  'shell.signOut': 'Se déconnecter',
  'shell.shortcuts': 'Raccourcis',
  'shell.shortcutsTitle': 'Raccourcis clavier ( ? )',
  'shell.language': 'Langue',
  'shell.appearance': 'Apparence',

  'nav.dashboard': 'Ce mois-ci',
  'nav.groupSales': 'Ventes',
  'nav.groupService': 'Atelier',
  'nav.groupAccounting': 'Comptabilité',
  'nav.groupPeople': 'Personnel et sécurité',
  'nav.customers': 'Clients',
  'nav.leads': 'Demandes',
  'nav.deals': 'Ventes',
  'nav.stock': 'Stock',
  'nav.workshop': 'Atelier',
  'nav.parts': 'Pièces',
  'nav.trialBalance': 'Balance générale',
  'nav.books': 'Les comptes',
  'nav.records': 'Données',
  'nav.staff': 'Personnel',
  'nav.secondFactor': 'Connexion en deux étapes',
  'nav.passkeys': 'Clés d’accès',

  'appearance.auto': 'Auto',
  'appearance.autoHint': 'Suivre cet appareil',
  'appearance.light': 'Clair',
  'appearance.lightHint': 'Toujours clair',
  'appearance.dark': 'Sombre',
  'appearance.darkHint': 'Toujours sombre',

  'signIn.lede': 'Connectez-vous à votre concession.',
  'signIn.dealerGroup': 'Groupe de concessions',
  'signIn.email': 'E-mail',
  'signIn.password': 'Mot de passe',
  'signIn.submit': 'Se connecter',
  'signIn.submitting': 'Connexion…',
  'signIn.codeLede':
    'Saisissez le code à six chiffres de votre application d’authentification, ou l’un de vos codes de secours.',
  'signIn.code': 'Code',
  'signIn.checking': 'Vérification…',
  'signIn.startAgain': 'Recommencer',

  'setPassword.title': 'Définissez votre mot de passe',
  'setPassword.lede':
    'Votre responsable vous a remis un code. Utilisez-le une seule fois ici pour choisir un mot de passe connu de vous seul — personne dans la concession ne peut voir ce que vous choisissez.',
  'setPassword.dealership': 'Concession',
  'setPassword.email': 'E-mail',
  'setPassword.code': 'Code',
  'setPassword.password': 'Nouveau mot de passe',
  'setPassword.passwordHint':
    'Au moins 12 caractères. C’est la longueur qui rend un mot de passe difficile à deviner.',
  'setPassword.again': 'Confirmez le nouveau mot de passe',
  'setPassword.mismatch': 'Ces deux mots de passe ne correspondent pas.',
  'setPassword.failed': 'Cela n’a pas fonctionné.',
  'setPassword.submit': 'Définir mon mot de passe',
  'setPassword.doneTitle': 'Votre compte est prêt',
  'setPassword.doneLede':
    'Connectez-vous avec votre adresse e-mail et le mot de passe que vous venez de choisir.',
  'setPassword.toSignIn': 'Aller à la connexion',

  'secondFactor.title': 'Connexion en deux étapes',
  'secondFactor.required':
    'Votre concession exige la connexion en deux étapes pour votre rôle. Tant que vous ne l’avez pas configurée, cet écran est le seul que vous pouvez utiliser.',
  'secondFactor.intro':
    'Ensuite, la connexion demandera un code à six chiffres généré par une application sur votre téléphone, en plus de votre mot de passe. Google Authenticator, Authy et 1Password conviennent tous.',
  'secondFactor.start': 'Commencer',
  'secondFactor.starting': 'Démarrage…',
  'secondFactor.pointApp': 'Pointez votre application d’authentification vers ce carré.',
  'secondFactor.qrTitle': 'Scannez ceci avec votre application d’authentification',
  'secondFactor.cannotScan': 'Impossible de le scanner ?',
  'secondFactor.typeInstead': 'Saisissez plutôt ceci à la main dans l’application :',
  'secondFactor.enterCode': 'Saisissez maintenant le code affiché',
  'secondFactor.turnOn': 'Activer',
  'secondFactor.checking': 'Vérification…',
  'secondFactor.notYet':
    'Rien n’a encore changé pour la connexion. Cela ne prendra effet qu’une fois le code ci-dessus accepté.',
  'secondFactor.onNow':
    'La connexion en deux étapes est activée. Désormais, un code vous sera demandé après votre mot de passe.',
  'secondFactor.saveTitle': 'Conservez-les en lieu sûr',
  'secondFactor.saveLede':
    'Chacun ne fonctionne qu’une seule fois, et uniquement si vous perdez votre téléphone. C’est la seule fois où ils seront affichés.',
  'secondFactor.recoveryCodes': 'Codes de secours',

  'shortcuts.title': 'Raccourcis clavier',
  'shortcuts.space': 'Espace',
  'shortcuts.note':
    'Les raccourcis sont ignorés pendant que vous saisissez du texte, afin de ne jamais avaler un caractère que vous vouliez écrire.',
  'shortcuts.goDashboard': 'Aller au tableau de bord',
  'shortcuts.goStock': 'Aller au stock',
  'shortcuts.goCustomers': 'Aller aux clients',
  'shortcuts.goLeads': 'Aller aux demandes',
  'shortcuts.goDeals': 'Aller aux ventes',
  'shortcuts.goWorkshop': 'Aller à l’atelier',
  'shortcuts.goParts': 'Aller aux pièces',
  'shortcuts.goBooks': 'Aller aux comptes',
  'shortcuts.monthBefore': 'Sur le tableau de bord : le mois précédent',
  'shortcuts.monthAfter': 'Sur le tableau de bord : le mois suivant',
  'shortcuts.thisMonth': 'Sur le tableau de bord : revenir à ce mois-ci',
  'shortcuts.showList': 'Afficher cette liste',

  // Vocabulaire du métier : « en préparation » plutôt que « reconditionnement »,
  // « visite spontanée » plutôt que « walk-in », et une voiture retenue par une
  // vente est « réservée », pas « en attente ».
  'enum.inventoryStatus.Incoming': 'En arrivage',
  'enum.inventoryStatus.Reconditioning': 'En préparation',
  'enum.inventoryStatus.Available': 'Disponible',
  'enum.inventoryStatus.OnHold': 'Réservé',
  'enum.inventoryStatus.Sold': 'Vendu',
  'enum.inventoryStatus.Removed': 'Retiré',

  // Une demande est féminine, d’où les accords.
  'enum.leadStatus.New': 'Nouvelle',
  'enum.leadStatus.Working': 'En cours',
  'enum.leadStatus.Appointment': 'Rendez-vous',
  'enum.leadStatus.Won': 'Gagnée',
  'enum.leadStatus.Lost': 'Perdue',

  'enum.leadSource.WalkIn': 'Visite spontanée',
  'enum.leadSource.Phone': 'Téléphone',
  'enum.leadSource.Website': 'Site web',
  'enum.leadSource.Referral': 'Recommandation',
  'enum.leadSource.Marketplace': 'Annonce en ligne',
  'enum.leadSource.Unknown': 'Non renseignée',

  'enum.dealStatus.Draft': 'Brouillon',
  'enum.dealStatus.Submitted': 'Soumise',
  'enum.dealStatus.Approved': 'Approuvée',
  'enum.dealStatus.Delivered': 'Livrée',
  'enum.dealStatus.Lost': 'Perdue',

  'enum.chargeKind.VehiclePrice': 'Prix du véhicule',
  'enum.chargeKind.Fee': 'Frais',
  'enum.chargeKind.Discount': 'Remise',
  'enum.chargeKind.Accessory': 'Accessoire',

  'enum.repairOrderStatus.Booked': 'Planifié',
  'enum.repairOrderStatus.InProgress': 'En cours',
  'enum.repairOrderStatus.Completed': 'Terminé',
  'enum.repairOrderStatus.Invoiced': 'Facturé',
  'enum.repairOrderStatus.Cancelled': 'Annulé',

  'enum.serviceLineKind.Labour': 'Main-d’œuvre',
  'enum.serviceLineKind.Part': 'Pièce',
  'enum.serviceLineKind.Sublet': 'Sous-traité',

  'enum.servicePayType.CustomerPay': 'Le client paie',
  'enum.servicePayType.Warranty': 'Garantie',
  'enum.servicePayType.Internal': 'Interne',

  'enum.financeProductKind.Warranty': 'Garantie',
  'enum.financeProductKind.Gap': 'GAP',
  'enum.financeProductKind.ServicePlan': 'Contrat d’entretien',
  'enum.financeProductKind.Protection': 'Protection',
  'enum.financeProductKind.Other': 'Autre',

  'enum.periodState.Open': 'Ouvert',
  'enum.periodState.Closed': 'Clôturé',

  'enum.booksState.NotOpened': 'Non ouvert',
  'enum.booksState.Open': 'Ouvert',
  'enum.booksState.Closed': 'Clôturé',
  'enum.booksState.Unknown': 'Inconnu',

  'enum.importKind.Customers': 'Clients',
  'enum.importKind.Vehicles': 'Véhicules',

  'enum.tenantStatus.Active': 'Actif',
  'enum.tenantStatus.Suspended': 'Suspendu',
  'enum.tenantStatus.Provisioning': 'En cours de création',
  'enum.tenantStatus.Archived': 'Archivé',

  'stock.title': 'Stock',
  'stock.status': 'Statut',
  'stock.loading': 'Chargement du stock…',
  'stock.denied':
    'Vous n’avez pas accès au stock de ce site. Demandez à un responsable si cela vous semble anormal.',
  'stock.failed': 'Impossible de charger le stock.',
  'stock.empty':
    'Rien pour l’instant. Les véhicules apparaissent une fois entrés en stock.',
  'stock.onlyStockNumber': 'Affichage du numéro de stock {stock} uniquement.',
  'stock.showEverything': 'Tout afficher',
  'stock.colStock': 'N° de stock',
  'stock.colVehicle': 'Véhicule',
  'stock.colVin': 'VIN',
  'stock.colStatus': 'Statut',
  'stock.count': { one: '{count} véhicule en stock', other: '{count} véhicules en stock' },
  'stock.detailFor': 'Numéro de stock {stock}',
  'stock.cost': 'Coût',
  'stock.costUnknown': 'non renseigné',
  'stock.acquired': 'Entré en stock',
  'stock.historyTitle': 'Ce qui s’est passé',
  'stock.historyEmpty': 'Rien n’a encore été enregistré pour cette voiture.',
  'stock.takenIn': 'Entré en stock comme {to}',
  'stock.moved': '{from} → {to}',

  'nav.reports': 'Rapports',

  'reports.title': 'Ce que le mois a rapporté',
  'reports.month': 'Mois',
  'reports.loading': 'Calcul des chiffres…',
  'reports.denied': 'Vous n’avez pas accès aux chiffres. Voyez avec la personne qui tient la comptabilité.',
  'reports.profitTitle': 'Compte de résultat',
  'reports.department': 'Département',
  'reports.revenue': 'Produits',
  'reports.cost': 'Coût',
  'reports.gross': 'Marge',
  'reports.grossProfit': 'Marge brute',
  'reports.overheads': 'Ce que coûte le fonctionnement',
  'reports.totalOverheads': 'Total des charges',
  'reports.netProfit': 'Résultat net',
  'reports.sheetTitle': 'Ce que vaut l’entreprise',
  'reports.sheetBalances': 'C’est équilibré : ce que l’entreprise possède égale ce qu’elle doit plus ce qu’elle vaut.',
  'reports.sheetDoesNotBalance':
    'Cela ne s’équilibre pas. Quelque chose a été comptabilisé que ce rapport ne sait pas classer : tenez tous les chiffres ci-dessous pour douteux tant que personne n’a trouvé quoi.',
  'reports.assets': 'Ce qu’elle possède',
  'reports.liabilities': 'Ce qu’elle doit',
  'reports.equity': 'Ce que les propriétaires ont apporté',
  'reports.total': 'Total',
  'reports.nothingHere': 'Rien n’est encore enregistré ici.',
  'reports.earningsToDate': 'Gagné depuis le début',
  'reports.earningsNote':
    'Gardé sur sa propre ligne plutôt qu’ajouté aux apports, car aucun exercice n’a encore été clôturé.',

  'entry.title': 'Saisir une écriture',
  'entry.note':
    'Pour ce que rien d’autre n’enregistre : une charge, l’apport des propriétaires, une correction. Les deux côtés doivent faire le même total.',
  'entry.whichLocation': 'Quel site',
  'entry.chooseLocation': 'Choisir un site…',
  'entry.when': 'Quand cela a eu lieu',
  'entry.what': 'À quoi cela correspond',
  'entry.account': 'Compte',
  'entry.chooseAccount': 'Choisir un compte…',
  'entry.debit': 'Débit',
  'entry.credit': 'Crédit',
  'entry.lineNote': 'Note',
  'entry.accountOnLine': 'Compte à la ligne {line}',
  'entry.debitOnLine': 'Débit à la ligne {line}',
  'entry.creditOnLine': 'Crédit à la ligne {line}',
  'entry.noteOnLine': 'Note à la ligne {line}',
  'entry.totals': 'Totaux',
  'entry.addLine': 'Ajouter une ligne',
  'entry.outBy': 'Les deux côtés diffèrent de {amount}.',
  'entry.record': 'Enregistrer',

  'enum.paymentMethod.Cash': 'Espèces',
  'enum.paymentMethod.Card': 'Carte',
  'enum.paymentMethod.BankTransfer': 'Virement',
  'enum.paymentMethod.Cheque': 'Chèque',
  'enum.paymentMethod.Finance': 'Organisme de financement',
  'enum.paymentMethod.CustomerCredit': 'Avoir client',

  'money.title': 'Ce qui reste dû',
  'money.billed': 'Facturé',
  'money.paid': 'Déjà payé',
  'money.outstanding': 'Reste à payer',
  'money.settled': 'Réglé en totalité. Il ne reste rien à payer.',
  'money.owedFor': {
    one: 'Dû depuis {count} jour.',
    other: 'Dû depuis {count} jours.',
  },
  'money.paymentsTitle': 'Ce qui a été payé',
  'money.howMuch': 'Montant',
  'money.howPaid': 'Mode de paiement',
  'money.reference': 'Référence (reste au dossier)',
  'money.takeIt': 'Enregistrer le paiement',
  'picker.change': 'Modifier',
  'picker.typeToSearch': 'Saisir pour rechercher',
  'picker.searching': 'Recherche…',
  'picker.searchFailed': 'La recherche n’a pas pu aboutir. Réessayez.',
  'picker.noMatches': 'Aucun résultat.',
  'picker.startTyping': 'Saisissez quelques lettres pour en trouver un.',
  'money.willOverpay':
    'C’est {extra} de plus que le montant dû. Le surplus devient un avoir dû au client.',
  'money.creditUsable': 'Ce client a de l’argent chez nous',
  'money.creditUsableNote':
    'Il a trop payé quelque chose auparavant. Cela peut venir en déduction de cette facture.',
  'money.useItHere': 'Affecter {amount} à cette facture',
  'money.creditTitle': 'Dû au client',
  'money.creditNote':
    'Cet argent est le sien, conservé ici jusqu’à utilisation ou remboursement. Il n’est pas à la concession.',
  'money.creditFrom': 'trop-perçu le {date}',
  'money.refundHow': 'Mode de remboursement',
  'money.giveItBack': 'Le rembourser',
  'stock.takeItIn': 'Entrer une voiture en stock',
  'stock.confirmTakeIn': 'Entrer en stock',
  'stock.takeInTitle': 'Entrer une voiture en stock',
  'stock.takeInNote':
    'La voiture et sa fiche sont créées ensemble, car une voiture qui arrive est presque toujours une que vous ne connaissez pas.',
  'stock.whichLocation': 'Quel site',
  'stock.chooseLocation': 'Choisir un site…',
  'stock.vinOptional': 'VIN (facultatif)',
  'stock.modelYear': 'Année',
  'stock.make': 'Marque',
  'stock.model': 'Modèle',
  'stock.trimOptional': 'Finition (facultatif)',
  'stock.costOptional': 'Ce qu’elle a coûté (facultatif)',
  'stock.costNote':
    'Un coût inscrit la voiture au bilan. Laissez vide si vous ne le connaissez pas encore : ce sera enregistré comme inconnu, pas comme zéro.',
  'stock.floorplanned': 'Un organisme finance cette voiture (floorplan)',
  'stock.moveTitle': 'Où elle va ensuite',
  'stock.moveNote': 'Note (reste au dossier)',
  'stock.moveTo': 'Passer à {to}',
  'stock.soldNote': 'Cette voiture est vendue. Contre-passez l’opération pour annuler.',
  'stock.noMovesNote': 'Cette voiture ne peut aller nulle part depuis ici.',

  // Vocabulaire du plan comptable général.
  'enum.accountKind.Asset': 'Actif',
  'enum.accountKind.Liability': 'Passif',
  'enum.accountKind.Equity': 'Capitaux propres',
  'enum.accountKind.Revenue': 'Produits',
  'enum.accountKind.Expense': 'Charges',

  'trialBalance.title': 'Balance générale',
  'trialBalance.loading': 'Calcul en cours…',
  'trialBalance.denied': 'Vous n’avez pas accès à ces chiffres.',
  'trialBalance.failed': 'Impossible de charger les soldes.',
  'trialBalance.empty':
    'Aucune écriture pour l’instant. Les écritures apparaissent ici une fois un véhicule livré.',
  'trialBalance.inBalance': 'Équilibrée — débits et crédits s’élèvent tous deux à {total}.',
  'trialBalance.outOfBalance':
    'Déséquilibre de {difference}. Quelque chose s’est perdu en cours de route.',
  'trialBalance.colCode': 'Numéro',
  'trialBalance.colAccount': 'Compte',
  'trialBalance.colKind': 'Nature',
  'trialBalance.colDebits': 'Débits',
  'trialBalance.colCredits': 'Crédits',
  'trialBalance.colBalance': 'Solde',
  'trialBalance.total': 'Total',

  'enum.customerKind.Person': 'Particulier',
  'enum.customerKind.Business': 'Entreprise',

  'enum.contactKind.Email': 'E-mail',
  'enum.contactKind.Phone': 'Téléphone',
  'enum.contactKind.Mobile': 'Portable',

  'customers.title': 'Clients',
  'customers.find': 'Rechercher quelqu’un',
  'customers.findPlaceholder': 'Nom, téléphone ou e-mail',
  'customers.add': 'Ajouter un client',
  'customers.looking': 'Recherche…',
  'customers.denied':
    'Vous n’avez pas accès aux fiches clients. Demandez à un responsable si cela vous semble anormal.',
  'customers.failed': 'Impossible de charger les clients.',
  'customers.noMatches': 'Personne ne correspond.',
  'customers.colName': 'Nom',
  'customers.colKind': 'Type',
  'customers.colEmail': 'E-mail',
  'customers.colPhone': 'Téléphone',
  'customers.count': { one: '{count} client', other: '{count} clients' },

  'customers.kindLabel': 'Particulier ou entreprise',
  'customers.firstName': 'Prénom',
  'customers.lastName': 'Nom',
  'customers.businessName': 'Raison sociale',
  'customers.email': 'E-mail',
  'customers.phone': 'Téléphone',
  'customers.submit': 'Ajouter',
  'customers.checking': 'Recherche de doublons…',
  'customers.adding': 'Ajout…',

  'customers.duplicateTitle': 'Quelqu’un de semblable existe déjà',
  'customers.duplicateLede':
    'Créer une seconde fiche pour la même personne divise son historique — son entretien, ses ventes et ses coordonnées cessent de concorder. Vérifiez si l’une de ces fiches est la bonne.',
  'customers.noContactDetails': 'aucune coordonnée',
  'customers.cameFrom': 'Provient de',
  'customers.waysToReach': 'Comment le joindre',
  'customers.primary': 'principal',
  'customers.address': 'Adresse',
  'customers.noAddress': 'Aucune adresse enregistrée.',
  'customers.oneOfTheseIsThem': 'C’est l’une de ces fiches',
  'customers.addAnyway': 'Aucune de celles-ci — ajouter quand même',

  'enum.importOutcome.Pending': 'En attente',
  'enum.importOutcome.Created': 'Ajoutée',
  'enum.importOutcome.Updated': 'Déjà présente',
  'enum.importOutcome.Skipped': 'Ignorée',
  'enum.importOutcome.Failed': 'Refusée',

  'records.title': 'Données',
  'records.bringIn': 'Importer des données',
  'records.bringInLede':
    'Un tableur exporté de votre ancien système, enregistré en CSV. Rien n’est écrit tant que vous n’avez pas fait un essai à blanc.',
  'records.whatIsInIt': 'Contenu du fichier',
  'records.file': 'Le fichier',
  'records.unreadableFile': 'Ce fichier n’a pas pu être lu. S’agit-il bien d’un CSV texte ?',
  'records.practice': 'Essai à blanc',
  'records.practising': 'Essai en cours…',
  'records.importForReal': 'Importer pour de bon',
  'records.importing': 'Importation…',
  'records.practiseFirst':
    'Faites d’abord l’essai à blanc. Il ne change rien et vous dit exactement ce que fera l’import réel.',

  'records.takeOut': 'Exporter des données',
  'records.takeOutLede':
    'Télécharge tout ce qui est de ce type au format CSV. C’est exactement la structure que cette page réaccepte : vous pouvez donc emporter vos données où vous voulez, y compris vers un autre système.',
  'records.downloadCustomers': 'Télécharger les clients',
  'records.downloadVehicles': 'Télécharger les véhicules',

  'records.couldNotRun': 'Cet import n’a pas pu être exécuté.',
  'records.whatWouldHappen': 'Ce qui se passerait',
  'records.whatHappened': 'Ce qui s’est passé',
  'records.summaryPractice': {
    one: 'Sur {count} ligne : {created} seraient ajoutées, {updated} déjà présentes, {skipped} ignorées, {failed} illisibles.',
    other:
      'Sur {count} lignes : {created} seraient ajoutées, {updated} déjà présentes, {skipped} ignorées, {failed} illisibles.',
  },
  'records.summaryReal': {
    one: 'Sur {count} ligne : {created} ajoutées, {updated} déjà présentes, {skipped} ignorées, {failed} refusées.',
    other:
      'Sur {count} lignes : {created} ajoutées, {updated} déjà présentes, {skipped} ignorées, {failed} refusées.',
  },
  'records.nothingWritten': 'Rien n’a été écrit. C’était un essai à blanc.',
  'records.rowsToLookAt': 'Lignes à examiner',
  'records.rowsToLookAtLede':
    'Le numéro de ligne est celui que vous voyez dans votre tableur, et la ligne est citée telle qu’elle est arrivée. Corrigez le fichier et relancez — rien ici ne modifie ce que vous avez envoyé.',
  'records.problemCount': {
    one: '{count} ligne à examiner',
    other: '{count} lignes à examiner',
  },
  'records.colLine': 'Ligne',
  'records.colWhatHappened': 'Ce qui s’est passé',
  'records.colTheRow': 'La ligne',
  'records.timeout':
    'Cet import prend plus de temps que prévu. Il est toujours en cours — c’est simplement cette page qui a cessé d’attendre.',

  'periods.title': 'Les comptes',
  'periods.loading': 'Chargement des comptes…',
  'periods.denied': 'Vous n’avez pas accès à la comptabilité.',
  'periods.failed': 'Impossible de lire les comptes.',
  'periods.actionFailed': 'Cela n’a pas fonctionné.',
  'periods.lede':
    'Rien ne peut être comptabilisé dans un mois tant que ses comptes ne sont pas ouverts, ni dans un mois clôturé. La clôture est un acte que vous effectuez lorsque les travaux de fin de mois sont terminés — aucune date ne la déclenche à votre place.',
  'periods.none': 'Aucun mois n’est encore ouvert. Rien ne peut être comptabilisé avant.',

  'periods.openAMonth': 'Ouvrir un mois',
  'periods.openLede':
    'Tant qu’un mois n’est pas ouvert, rien qui y soit daté ne peut être comptabilisé — une vente ou une facture d’atelier sera refusée. L’ouverture est délibérée, afin que les comptes commencent à une date que vous avez choisie plutôt qu’à celle déduite de la première saisie venue.',
  'periods.year': 'Année',
  'periods.month': 'Mois',
  'periods.openIt': 'Ouvrir',

  'periods.reopenTitle': 'Rouvrir {month} ?',
  'periods.reopenLede':
    'Ce mois est clôturé et ses chiffres ont peut-être déjà été communiqués. Sa réouverture est consignée avec votre motif, afin que quiconque le consultera plus tard sache ce qui s’est passé et pourquoi.',
  'periods.reopenWhy': 'Pourquoi le rouvrir ?',
  'periods.reopenPlaceholder': 'Une facture fournisseur est arrivée le 4',
  'periods.reopenIt': 'Rouvrir',
  'periods.leaveClosed': 'Le laisser clôturé',
  'periods.reopen': 'Rouvrir',

  'periods.closeIt': 'Clôturer',
  'periods.closeTitle': 'Clôturer {month} ?',
  'periods.confirmClose':
    'Plus rien ne pourra y être comptabilisé tant qu’il ne sera pas rouvert.',

  'periods.caption': 'Tous les mois des comptes, du plus récent au plus ancien.',
  'periods.colMonth': 'Mois',
  'periods.colCutoff': 'Date d’arrêté',
  'periods.colEntries': 'Écritures',
  'periods.colState': 'État',

  'periods.historyTitle': 'Ce qui est arrivé aux comptes',
  'periods.wasOpened': '{month} ouvert',
  'periods.wasClosed': '{month} clôturé',
  'periods.wasReopened': '{month} rouvert',

  'leads.title': 'Demandes',
  'leads.show': 'Afficher',
  'leads.stillChasing': 'En cours de suivi',
  'leads.everything': 'Tout',
  'leads.onlyMine': 'Seulement les miennes',
  'leads.take': 'Enregistrer une demande',
  'leads.untouchedTitle': 'Personne ne s’en occupe',
  'leads.untouchedNote': {
    one: '{count} demande n’a personne à son nom.',
    other: '{count} demandes n’ont personne à leur nom.',
  },
  'leads.noParticularCar': 'aucune voiture précise',
  'leads.waitingDays': {
    one: 'en attente depuis {count} jour',
    other: 'en attente depuis {count} jours',
  },
  'leads.loading': 'Chargement des demandes…',
  'leads.denied':
    'Vous n’avez pas accès aux demandes de ce site. Demandez à un responsable si cela vous semble anormal.',
  'leads.failed': 'Impossible de charger les demandes.',
  'leads.openFailed': 'Impossible d’ouvrir cette demande.',
  'leads.empty':
    'Aucune demande ici. Il y en a une dès que quelqu’un appelle ou se présente sur le parc.',

  'leads.colCustomer': 'Client',
  'leads.colAskedAbout': 'Véhicule demandé',
  'leads.colCameFrom': 'Origine',
  'leads.colDays': 'Jours',
  'leads.colChasedBy': 'Suivi par',
  'leads.colStage': 'Étape',
  'leads.nothingSpecific': 'Rien de précis',
  'leads.nobodyYet': 'Personne pour l’instant',
  'leads.you': 'Vous',
  'leads.somebodyElse': 'Quelqu’un d’autre',
  'leads.count': { one: '{count} demande', other: '{count} demandes' },
  'paging.showingRange': 'Affichage de {first} à {last} sur {total}.',
  'paging.previous': 'Précédents',
  'paging.next': 'Suivants',

  'leads.cameIn': 'reçue le {date}',
  'leads.unclaimed': 'Personne ne s’en occupe encore.',
  'leads.yoursToChase': 'C’est vous qui suivez celle-ci.',
  'leads.theirsToChase': 'C’est {name} qui suit celle-ci.',
  'leads.putBack': 'La remettre dans le pot commun',
  'leads.iWillChase': 'Je m’en occupe',
  'leads.takeItOver': 'La reprendre',
  'leads.handTo': 'Confier à',
  'leads.chooseColleague': 'Choisir un collègue',
  'leads.buildTheDeal': 'Créer la vente',
  'leads.whatHappened': 'Historique',
  'leads.finished':
    'Cette demande est terminée. Si le client revient plus tard, cela en ouvre une nouvelle.',
  'leads.note': 'Note (conservée au dossier)',
  'leads.notePlaceholder': 'Message laissé · passe samedi · a acheté ailleurs',
  'leads.reopenedHere':
    'Une demande perdue qui revient est rouverte ici plutôt que ressaisie, afin que la première tentative reste dans l’historique.',

  'leads.moveReopen': 'La rouvrir',
  'leads.moveStartChasing': 'Commencer le suivi',
  'leads.moveAppointment': 'Le client vient',
  'leads.moveWon': 'Le client achète',
  'leads.moveLost': 'Marquer comme perdue',

  'leads.captureTitle': 'Enregistrer une demande',
  'leads.findCustomer': 'Trouver le client',
  'leads.whoIsAsking': 'Qui demande',
  'leads.chooseSomebody': 'Choisir une personne…',
  'leads.searchAboveNote':
    'Cherchez ci-dessus pour la trouver. Une demande doit être rattachée à quelqu’un : si la personne est nouvelle, créez-la d’abord sur la page Clients.',
  'leads.notOnFile': 'Pas au fichier ? Ajoutez-le ici.',
  'leads.newCustomerTitle': 'Quelqu’un de nouveau',
  'leads.addAndUse': 'Ajouter et utiliser',
  'leads.whichLocation': 'Quel site',
  'leads.chooseLocation': 'Choisir un site…',
  'leads.onlyLocation':
    'Cette demande est rattachée à {name} ({code}), le seul site où vous travaillez.',
  'leads.howTheyReachedUs': 'Comment le client nous a contactés',
  'leads.carAskedAbout': 'Véhicule demandé (facultatif)',
  'leads.whatTheySaid': 'Ce que le client a dit',
  'leads.whatTheySaidPlaceholder': 'Budget, reprise, échéance souhaitée…',
  'leads.save': 'Enregistrer la demande',
  'leads.locationsFailed': 'Impossible de charger vos sites.',
  'leads.lookupFailed': 'Impossible d’effectuer cette recherche.',
  'leads.saveFailed': 'Cette demande n’a pas pu être enregistrée.',

  'deals.title': 'Ventes',
  'deals.show': 'Afficher',
  'deals.stillWorked': 'En cours',
  'deals.everything': 'Tout',
  'deals.start': 'Créer une vente',
  'deals.loading': 'Chargement des ventes…',
  'deals.denied':
    'Vous n’avez pas accès aux ventes de ce site. Demandez à un responsable si cela vous semble anormal.',
  'deals.failed': 'Impossible de charger les ventes.',
  'deals.openFailed': 'Impossible d’ouvrir cette vente.',
  'deals.empty': 'Aucune vente ici. Il y en a une dès qu’un véhicule est chiffré pour quelqu’un.',
  'deals.documentFailed': 'Le document n’a pas pu être ouvert.',

  'deals.colCustomer': 'Client',
  'deals.colVehicle': 'Véhicule',
  'deals.colStock': 'N° de stock',
  'deals.colDue': 'Reste dû',
  'deals.colStage': 'Étape',
  'deals.count': { one: '{count} vente', other: '{count} ventes' },

  'deals.printOrder': 'Imprimer le bon de commande',
  'deals.stockLine': 'Stock {stock}',
  'deals.numbersCaption': 'Le chiffrage de cette vente',
  'deals.colLine': 'Ligne',
  'deals.colDescription': 'Désignation',
  'deals.colAmount': 'Montant',
  'deals.lineProduct': 'Produit',
  'deals.lineTradeIn': 'Reprise',
  'deals.owesMore': 'doit plus que la valeur du véhicule',
  'deals.dueFromCustomer': 'Reste dû par le client',
  'deals.frozen':
    'Le chiffrage est figé. Il a cessé d’être modifiable au moment de la soumission, afin que le responsable approuve exactement ce qui lui a été présenté.',
  'deals.whatHappened': 'Historique',

  'deals.finished': 'Cette vente est terminée. Plus rien ne peut lui arriver.',
  'deals.sendToManager': 'Soumettre au responsable',
  'deals.approve': 'Approuver',
  'deals.handOver': 'Livrer le véhicule',
  'deals.markLost': 'Marquer comme perdue',
  'deals.markedLostNote': 'Marquée perdue depuis le bureau des ventes.',
  'deals.cannotApproveOwn':
    'Celui qui a monté cette vente ne peut pas être celui qui l’approuve. Si c’est vous, un responsable doit le faire.',

  'deals.soldWithTheCar': 'Vendu avec le véhicule',
  'deals.colProduct': 'Produit',
  'deals.colPrice': 'Prix',
  'deals.colGross': 'Marge',
  'deals.productGross': '{amount} de marge sur ce qui a été vendu avec le véhicule.',

  'terms.title': 'Le chiffrage',
  'terms.caption': 'Les lignes de cette vente',
  'terms.colLine': 'Ligne',
  'terms.colDescription': 'Désignation',
  'terms.colAmount': 'Montant',
  'terms.remove': 'Supprimer',
  'terms.lineKind': 'Type de la ligne {n}',
  'terms.lineDescription': 'Désignation de la ligne {n}',
  'terms.lineAmount': 'Montant de la ligne {n}',
  'terms.addLine': 'Ajouter une ligne',
  'terms.addTradeIn': 'Ajouter une reprise',
  'terms.dropTradeIn': 'Finalement pas de reprise',
  'terms.tradeInTitle': 'La reprise',
  'terms.whatTheyTrade': 'Véhicule repris',
  'terms.whatWeAllow': 'Valeur de reprise accordée',
  'terms.whatIsOwed': 'Solde restant dû dessus',
  'terms.negativeEquity':
    'Le solde restant dû dépasse la valeur de reprise accordée : la différence est ajoutée à cette vente.',
  'terms.save': 'Enregistrer le chiffrage',
  'terms.rejected': 'Ce chiffrage n’a pas été accepté.',
  'terms.needsPrice':
    'Toute vente doit comporter un prix pour le véhicule lui-même avant de pouvoir être enregistrée.',

  'products.title': 'Vendu avec le véhicule',
  'products.loading': 'Chargement de ce qui peut être vendu…',
  'products.none':
    'Aucun produit n’est paramétré à la vente. Un responsable les ajoute dans le catalogue F&I.',
  'products.lede':
    'Les prix proviennent du catalogue et vous pouvez les modifier — ce que vous saisissez ici est ce qui sera enregistré sur cette vente, et les évolutions ultérieures du tarif n’y toucheront pas.',
  'products.colSell': 'Vendre',
  'products.colProduct': 'Produit',
  'products.colPrice': 'Prix',
  'products.colCost': 'Coût',
  'products.colGross': 'Marge',
  'products.sellThis': 'Vendre {product}',
  'products.priceFor': 'Prix de {product}',
  'products.costOf': 'Coût de {product}',
  'products.termMonths': { one: '{count} mois', other: '{count} mois' },
  'products.withdrawn': 'plus proposé',
  'products.nothingSelected': 'Rien de sélectionné.',
  'products.addedToDeal': '{added} ajoutés à la vente, pour {gross} de marge.',
  'products.saveFailed': 'L’enregistrement a échoué.',
  'products.saveWhatIsSold': 'Enregistrer ce qui est vendu',

  'tax.title': 'Taxes',
  'tax.lede':
    "Rien ne calcule encore cela pour vous : saisissez ce qui s'applique. Chaque ligne indique qu'une personne l'a saisie, ce qu'un responsable et un auditeur doivent pouvoir voir ensuite.",
  'tax.workedOutFrom': 'Adresse servant au calcul des taxes',
  'tax.state': 'État ou région',
  'tax.county': 'Comté',
  'tax.postalCode': 'Code postal',
  'tax.country': 'Pays',
  'tax.colDescription': 'Taxe',
  'tax.colJurisdiction': 'Juridiction',
  'tax.colBasis': 'Assiette',
  'tax.colRate': 'Taux %',
  'tax.colAmount': 'Montant',
  'tax.colSource': 'Origine',
  'tax.descriptionOfLine': 'Taxe à la ligne {line}',
  'tax.jurisdictionOfLine': 'Juridiction à la ligne {line}',
  'tax.basisOfLine': 'Assiette à la ligne {line}',
  'tax.rateOfLine': 'Taux à la ligne {line}, en pourcentage',
  'tax.amountOfLine': 'Taxe facturée à la ligne {line}',
  'tax.none': 'Aucune taxe sur cette affaire pour le moment.',
  'tax.totalIs': 'Taxes sur cette affaire : {total}.',
  'tax.totalLabel': 'Taxes',
  'tax.addLine': 'Ajouter une ligne de taxe',
  'tax.save': 'Enregistrer les taxes',
  'tax.from.EnteredByPerson': 'saisie par une personne',
  'tax.from.Pack': 'une table de taux',
  'tax.from.Vendor': 'un prestataire fiscal',
  'tax.fromPack': '{pack} v{version}',

  'startDeal.title': 'Créer une vente',
  'startDeal.findBuyer': 'Trouver l’acheteur',
  'startDeal.buyer': 'Qui achète',
  'startDeal.chooseBuyer': 'Choisir une personne…',
  'startDeal.searchAbove':
    'Cherchez ci-dessus pour la trouver. Créez-la sur la page Clients si elle est nouvelle.',
  'startDeal.fromEnquiry': 'Issue de la demande de {name}. La vente y sera rattachée.',
  'startDeal.thatCustomer': 'ce client',
  'startDeal.whichCar': 'Quel véhicule',
  'startDeal.chooseCar': 'Choisir un véhicule…',
  'startDeal.nothingAvailable':
    'Aucun véhicule n’est disponible pour l’instant. Un véhicule déjà engagé sur une autre vente est réservé jusqu’à la clôture de celle-ci.',
  'startDeal.chooseCarFirst': 'Choisissez d’abord un véhicule.',
  'startDeal.submit': 'Créer la vente',
  'startDeal.starting': 'Création…',
  'startDeal.failed': 'Cette vente n’a pas pu être créée.',
  'startDeal.stockFailed': 'Impossible de charger le stock.',
  'startDeal.customerFailed': 'Impossible de lire cette fiche client.',

  'staff.title': 'Personnel',
  'staff.loading': 'Chargement des personnes qui travaillent ici…',
  'staff.denied':
    'Vous n’avez pas accès à la liste du personnel. Demandez-la à un responsable si vous en avez besoin.',
  'staff.failed': 'Impossible de lire la liste du personnel.',
  'staff.actionFailed': 'Cela n’a pas fonctionné.',
  'staff.empty': 'Personne pour l’instant.',
  'staff.add': 'Ajouter quelqu’un',
  'staff.caption': 'Toutes les personnes dont l’accès couvre un site où vous travaillez.',
  'staff.colName': 'Nom',
  'staff.colEmail': 'E-mail',
  'staff.colHolds': 'Droits',
  'staff.colSecondFactor': 'Deuxième facteur',
  'staff.colState': 'État',
  'staff.holdsNothing': 'Aucun pour l’instant',

  'staff.stateStopped': 'Désactivé',
  'staff.stateAwaiting': 'En attente du premier mot de passe',
  'staff.stateWorking': 'En poste',

  'staff.codeFor': 'Code pour {name}',
  'staff.readItOut':
    'Lisez-le-lui. C’est avec ce code qu’elle définira son propre mot de passe sur l’écran de connexion — personne d’autre ne le saisit jamais, vous compris.',
  'staff.onlyTimeShown': 'C’est la seule fois où il peut être affiché.',
  'staff.onlyTimeShownRest':
    'Seule son empreinte est conservée : il ne peut donc pas être retrouvé. S’il s’égare, émettez-en un nouveau, ce qui annule celui-ci. Il expire le {expires}.',
  'staff.passedItOn': 'Je le lui ai transmis',

  'staff.addTitle': 'Ajouter quelqu’un',
  'staff.addLede':
    'Cette personne ne pourra pas se connecter tant qu’elle n’aura pas défini un mot de passe avec le code généré ici. Vous ne voyez ni ne choisissez jamais son mot de passe.',
  'staff.name': 'Nom',
  'staff.email': 'E-mail',
  'staff.addAndMakeCode': 'Ajouter et générer un code',

  'staff.hasSecondFactor': 'possède un deuxième facteur',
  'staff.noSecondFactor': 'pas de deuxième facteur',
  'staff.whatTheyHold': 'Ses droits',
  'staff.holdsNothingYet': 'Aucun pour l’instant : elle peut se connecter et ne rien voir.',
  'staff.everywhere': 'partout',
  'staff.oneLocation': 'un site',
  'staff.takeItAway': 'Retirer',
  'staff.giveARole': 'Lui attribuer un rôle',
  'staff.role': 'Rôle',
  'staff.chooseRole': 'Choisir un rôle',
  'staff.holdingGrants': 'Ce rôle donne accès à : {permissions}',
  'staff.andObligesSecondFactor': ' — et l’oblige à configurer un deuxième facteur.',
  'staff.where': 'Où',
  'staff.everywhereInOrg': 'Partout dans le groupe',
  'staff.giveThem': 'Attribuer',
  'staff.makeNewCode': 'Générer un nouveau code',
  'staff.stopAccount': 'Désactiver ce compte',
  'staff.letThemBackIn': 'Réactiver le compte',
  'staff.stoppingNote':
    'Désactiver un compte met fin à ses sessions dès la requête suivante et ne supprime rien — son nom doit continuer d’apparaître en regard du travail effectué.',

  'parts.title': 'Pièces',
  'parts.loading': 'Chargement du catalogue de pièces…',
  'parts.denied':
    'Vous n’avez pas accès aux pièces de ce site. Demandez à un responsable si cela vous semble anormal.',
  'parts.failed': 'Impossible de lire le catalogue.',
  'parts.actionFailed': 'Cela n’a pas fonctionné.',
  'parts.add': 'Ajouter une pièce',
  'parts.find': 'Rechercher une pièce',
  'parts.findPlaceholder': 'Référence ou désignation',
  'parts.findHint':
    'La référence est reconnue quelle que soit la façon dont elle est saisie : MZ-690411, mz690411 et MZ 690 411 trouvent la même pièce.',
  'parts.catalogueEmpty': 'Le catalogue est encore vide.',
  'parts.noMatches': 'Aucun résultat.',
  'parts.colNumber': 'Référence',
  'parts.colDescription': 'Désignation',
  'parts.colWhere': 'Où',
  'parts.colOnHand': 'En stock',
  'parts.colCostEach': 'Coût unitaire',
  'parts.notStocked': 'Non stockée',
  'parts.oneLocation': 'un site',
  'parts.noneOnHand': 'Aucune',

  'parts.costingTitle': 'Méthode de valorisation des pièces',
  'parts.costingMethod': 'Méthode',
  'parts.costingFutureOnly': 'aux ventes futures uniquement',
  'parts.costingNote':
    'Les travaux déjà facturés conservent le coût auquel ils ont été vendus — modifier ce réglage ne peut pas retraiter un mois déjà publié.',
  'parts.costingApplies': 'Cela s’applique {futureOnly}. {rest}',

  'parts.addTitle': 'Ajouter une pièce',
  'parts.addLede':
    'Une référence désigne le même composant sur tous les sites : c’est donc une modification au niveau du groupe. Le stock, lui, appartient au magasin sur lequel il est réceptionné.',
  'parts.partNumber': 'Référence',
  'parts.description': 'Désignation',
  'parts.addIt': 'Ajouter',

  'parts.noneVisible': 'Aucune sur un magasin visible pour vous.',
  'parts.shelfHeading': '{code} — {quantity} en stock à {cost} l’unité',
  'parts.deliveriesCaption': 'Réceptions de {part} sur {code}.',
  'parts.colReceived': 'Réception',
  'parts.colNote': 'Note',
  'parts.colCameIn': 'Entrées',
  'parts.colLeft': 'Restant',

  'parts.bookIn': 'Réceptionner une livraison',
  'parts.ontoWhichShelf': 'Sur quel magasin',
  'parts.howMany': 'Quantité',
  'parts.costEach': 'Coût unitaire',
  'parts.deliveryNote': 'Bon de livraison',
  'parts.bookItIn': 'Réceptionner',

  'dash.soFar': 'Depuis le début du mois.',
  'dash.asFinished': 'Le mois tel qu’il s’est terminé.',
  'dash.whichMonth': 'Quel mois',
  'dash.previousMonth': 'Mois précédent',
  'dash.nextMonth': 'Mois suivant',
  'dash.previousMonthTitle': 'Mois précédent ( [ )',
  'dash.nextMonthTitle': 'Mois suivant ( ] )',
  'dash.rooftop': 'Site',
  'dash.everywhere': 'Tous les sites visibles',
  'dash.loading': 'Calcul du mois…',
  'dash.denied': 'Vous n’avez accès à aucun des chiffres de ce tableau de bord.',
  'dash.failed': 'Impossible de charger le mois.',

  'dash.withheldTrading':
    'Les montants de ce mois ne vous sont pas accessibles : les chiffres ci-dessous ne concernent que le stock.',
  'dash.withheldStock':
    'Le stock ne vous est pas accessible : ce mois n’affiche donc que ce qui a été vendu.',

  'dash.booksOpen': 'Les comptes sont ouverts, ces chiffres peuvent donc encore bouger.',
  'dash.booksClosed': 'Les comptes sont clôturés. Voici les chiffres qui ont été communiqués.',
  'dash.booksClosedOn':
    'Les comptes ont été clôturés le {date}. Voici les chiffres qui ont été communiqués.',
  'dash.booksNotOpened':
    'Personne n’a ouvert les comptes de ce mois : rien ne peut y être comptabilisé.',
  'dash.booksUnknown': 'L’état des comptes ne vous est pas accessible.',

  'dash.whatTheMonthMade': 'Ce que le mois a rapporté',
  'dash.totalGross': 'Marge totale',
  'dash.financeShort': 'F&I',
  'dash.whatSold': 'Ce qui a été vendu',
  'dash.carsDelivered': 'Véhicules livrés',
  'dash.jobsInvoiced': 'Ordres de réparation facturés',
  'dash.grossPerCar': 'Marge par véhicule',
  'dash.frontAndBack': 'véhicule et F&I confondus',

  'dash.whereGrossCameFrom': 'D’où vient la marge',
  'dash.colDepartment': 'Service',
  'dash.colRevenue': 'Chiffre d’affaires',
  'dash.colCost': 'Coût',
  'dash.colGross': 'Marge',
  'dash.colMargin': 'Taux',
  'dash.total': 'Total',

  'dash.howOldTheStockIs': 'Ancienneté du stock',
  'dash.unsoldAsAt': ' — {count} invendus, au {date}',
  'dash.nothingUnsold': 'Aucun véhicule invendu sur le parc.',
  'dash.standingLongest': 'Les plus anciens',
  'dash.colStock': 'N° de stock',
  'dash.colVehicle': 'Véhicule',
  'dash.colStatus': 'Statut',
  'dash.colDays': 'Jours',
  'dash.estimatedAge':
    'Aucune date d’acquisition n’a été enregistrée : le décompte part de la date de saisie.',
  'dash.estimatedAgeNote':
    '* décompté depuis la saisie du véhicule, faute de date d’acquisition enregistrée.',

  'workshop.title': 'Atelier',
  'workshop.loading': 'Chargement de l’atelier…',
  'workshop.denied':
    'Vous n’avez pas accès à l’atelier de ce site. Demandez à un responsable si cela vous semble anormal.',
  'workshop.failed': 'Impossible de lire la liste de l’atelier.',
  'workshop.actionFailed': 'Cela n’a pas fonctionné.',
  'workshop.openOnly': 'Uniquement les OR en cours',
  'workshop.nothingOpen': 'Rien à l’atelier pour le moment.',
  'workshop.empty': 'Aucun ordre de réparation ici.',
  'workshop.caption': 'Les ordres de réparation des sites que vous couvrez.',
  'workshop.colJob': 'OR',
  'workshop.colCustomer': 'Client',
  'workshop.colVehicle': 'Véhicule',
  'workshop.colCameInFor': 'Motif d’entrée',
  'workshop.colWaiting': 'En attente',
  'workshop.colDue': 'Montant dû',
  'workshop.colStage': 'Étape',

  'workshop.waitingTitle': 'En attente du client',
  'workshop.waitingNote': {
    one: 'Un ordre de réparation comporte des travaux que personne n’a encore acceptés. Il ne peut pas être facturé tant que quelqu’un n’a pas appelé.',
    other:
      '{count} ordres de réparation comportent des travaux que personne n’a encore acceptés. Aucun ne peut être facturé tant que quelqu’un n’a pas appelé.',
  },
  'workshop.toAskAbout': {
    one: '{count} travail à faire valider',
    other: '{count} travaux à faire valider',
  },
  'workshop.pendingNote': {
    one: 'Un travail est en attente de l’accord du client. Il ne peut pas être facturé avant sa réponse.',
    other:
      '{count} travaux sont en attente de l’accord du client. Aucun ne peut être facturé avant sa réponse.',
  },

  'workshop.stageBooked': 'Planifié',
  'workshop.stageInProgress': 'En cours de réparation',
  'workshop.stageCompleted': 'Travaux terminés',
  'workshop.stageInvoiced': 'Facturé',
  'workshop.stageCancelled': 'Annulé',

  'workshop.moveInProgress': 'Commencer les travaux',
  'workshop.moveCompleted': 'Les travaux sont terminés',
  'workshop.moveInvoiced': 'Facturer',
  'workshop.moveCancelled': 'Annuler l’OR',
  'workshop.moveBooked': 'Revenir à planifié',

  'workshop.miles': '{count} miles',
  'workshop.bookedIn': 'entré le {date}',
  'workshop.printJobSheet': 'Imprimer l’ordre de réparation',
  'workshop.printInvoice': 'Imprimer la facture',
  'workshop.whatHappened': 'Historique',

  'workshop.theWork': 'Les travaux',
  'workshop.nothingWrittenUp': 'Rien n’a encore été saisi.',
  'workshop.colWhat': 'Nature',
  'workshop.colDetail': 'Détail',
  'workshop.colAgreed': 'Accepté ?',
  'workshop.colAmount': 'Montant',
  'workshop.nobodyAsked': 'Client non contacté',
  'workshop.saidNo': 'Refusé',
  'workshop.agreed': 'Accepté',
  'workshop.iRangThem': 'Je l’ai appelé',
  'workshop.notNow': 'Pas maintenant',
  'workshop.howObtained': 'Comment l’accord a été obtenu',
  'workshop.howObtainedPlaceholder': 'Appel à 10h40, Mme Okafor',
  'workshop.theySaidYes': 'Le client accepte',
  'workshop.theySaidNo': 'Le client refuse',
  'workshop.hoursAtRate': '{hours} h à {rate}',

  'workshop.writeUpMore': 'Saisir d’autres travaux',
  'workshop.lineKind': 'Nature',
  'workshop.lineDescription': 'Désignation',
  'workshop.lineJob': 'Travail du catalogue',
  'workshop.lineJobNote':
    'Facultatif. En choisir un remplit le temps standard et le tarif de ce site ; ce que vous saisissez prime.',
  'workshop.setupLink': 'Configurer l’atelier',
  'workshop.clockTitle': 'Temps passé sur ce travail',
  'workshop.clockedSoFar': '{hours} heures pointées à ce jour. Un technicien encore sur le travail ne compte pour rien tant qu’il n’a pas pointé la sortie.',
  'workshop.clockWho': 'Pointer l’entrée de quelqu’un…',
  'workshop.clockOn': 'Pointer l’entrée',
  'workshop.clockOff': 'Pointer la sortie',
  'workshop.onSince': 'depuis {since}',
  'workshop.someone': 'Quelqu’un ici',
  'labour.hoursClocked': 'Heures pointées',
  'labour.productivity': 'Productivité',
  'labour.colClocked': 'Pointées',
  'labour.colProductivity': 'Productivité',
  'labour.notClocked': 'Non pointé',

  'serviceSetup.title': 'Configuration atelier',
  'serviceSetup.loading': 'Chargement de la configuration…',
  'serviceSetup.denied': 'Vous ne pouvez pas voir l’atelier.',
  'serviceSetup.backToWorkshop': 'Retour à l’atelier',
  'serviceSetup.ratesTitle': 'Coût de l’heure',
  'serviceSetup.ratesNote':
    'Défini par site. Deux sites ne facturent pas forcément pareil, et la garantie est remboursée au tarif du constructeur.',
  'serviceSetup.location': 'Site',
  'serviceSetup.chooseLocation': 'Choisir un site…',
  'serviceSetup.perHour': 'Par heure',
  'serviceSetup.notSet': 'Non défini',
  'serviceSetup.setRate': 'Fixer le tarif',
  'serviceSetup.jobsTitle': 'Travaux vendus par l’atelier',
  'serviceSetup.jobsNote':
    'Partagés par tous les sites, comme une référence de pièce, pour que le même travail veuille dire la même chose partout.',
  'serviceSetup.code': 'Code',
  'serviceSetup.describes': 'Décrit',
  'serviceSetup.standardHours': 'Temps standard',
  'serviceSetup.whoPays': 'Qui paie normalement',
  'serviceSetup.withdraw': 'Retirer',
  'serviceSetup.restore': 'Rétablir',
  'serviceSetup.addJob': 'Ajouter le travail',
  'serviceSetup.frozenNote':
    'Rien n’est jamais supprimé ici. Retirer cesse de proposer un travail et laisse lisibles tous les ordres qui le citent déjà.',
  'workshop.hours': 'Heures',
  'workshop.rate': 'Taux horaire',
  'workshop.amount': 'Montant',
  'workshop.fromTheShelf': 'Pris en magasin',
  'workshop.notFromStock': 'Pas en stock (à saisir ci-dessous)',
  'workshop.howMany': 'Combien',
  'workshop.onTheShelf': {
    one: '{number} : {count} en magasin.',
    other: '{number} : {count} en magasin.',
  },
  'workshop.addLine': 'Ajouter',

  'workshop.totalsCaption': 'Le total de cet ordre de réparation.',
  'workshop.totalLabour': 'Main-d’œuvre',
  'workshop.totalParts': 'Pièces',
  'workshop.totalSublet': 'Sous-traitance',
  'workshop.totalDue': 'Montant dû',

  'workshop.whoIsOnIt': 'Qui s’en occupe',
  'workshop.nobodyYet': 'Personne pour l’instant',
  'workshop.invoicedNothingMore': 'Facturé le {date}. Le travail est terminé ; ce qui reste dû est ci-dessous.',
  'workshop.jobFinished': 'Cet ordre de réparation est terminé.',
  'workshop.assignedElsewhere':
    'Attribué à quelqu’un qui ne figure pas dans votre liste de personnel — cette personne travaille peut-être sur un autre site.',
  'workshop.removeLine': 'Retirer',
  'workshop.moveNote': 'Note (inscrite au dossier)',
  'workshop.howObtainedHint':
    'C’est ce qui compte si la facture est un jour contestée. Indiquez à qui vous avez parlé et quand.',
  'workshop.toAsk': {
    one: '{count} à demander',
    other: '{count} à demander',
  },
  'workshop.labourReport': 'Rapport de main-d’œuvre',

  'workshop.colWhoPays': 'Qui paie',
  'workshop.linePayType': 'Qui paie',
  'workshop.notCustomersCall': 'Ce n’est pas au client de l’accepter',
  'workshop.totalWarranty': 'Garantie',
  'workshop.totalInternal': 'Interne',
  'workshop.totalWork': 'Ensemble des travaux',
  'workshop.writeUpNote':
    'Tout ce qui est ajouté maintenant exige la réponse du client avant de pouvoir être facturé — c’est justement le but. Notez-le pendant que vous l’avez sous les yeux.',
  'workshop.writeUpNoteOther':
    'Personne n’a besoin d’appeler le client à ce sujet, puisque ce n’est pas lui qui paie. Cela figure quand même sur l’ordre de réparation, pour que le travail soit consigné et les heures comptées.',

  // --- Ce que l’atelier a vendu ---------------------------------------------
  'labour.title': 'Main-d’œuvre',
  'labour.from': 'Du',
  'labour.to': 'Au',
  'labour.backToWorkshop': 'Retour à l’atelier',
  'labour.loading': 'Calcul des chiffres de main-d’œuvre…',
  'labour.denied':
    'Vous n’avez pas accès aux chiffres de cet atelier. Voyez avec un responsable si cela vous semble anormal.',

  'labour.headlineCaption':
    'Heures vendues, chiffre d’affaires de main-d’œuvre et ce qu’une heure a réellement rapporté.',
  'labour.hoursSold': 'Heures vendues',
  'labour.revenue': 'Chiffre d’affaires main-d’œuvre',
  'labour.effectiveRate': 'Ce qu’une heure a rapporté',

  'labour.byTechnician': 'Par technicien',
  'labour.technicianCaption':
    'Heures et chiffre d’affaires de chaque technicien sur la période.',
  'labour.colWho': 'Technicien',
  'labour.colHours': 'Heures',
  'labour.colRevenue': 'Chiffre d’affaires',
  'labour.colRate': 'Par heure',
  'labour.nobodyCredited': 'Personne de crédité',
  'labour.notNamed': 'Sans nom',
  'labour.namesUnavailable':
    'Les techniciens ne sont pas nommés ici parce que vous ne pouvez pas consulter la liste du personnel. Les heures et les montants restent exacts.',
  'labour.nothingInvoiced':
    'Rien n’a été facturé sur cette période, il n’y a donc aucune heure à présenter.',

  'labour.byPayer': 'Qui a payé',
  'labour.payerCaption':
    'Heures et chiffre d’affaires, répartis selon qui règle les travaux.',
  'labour.colPayer': 'Réglé par',

  'labour.notMeasuredTitle': 'Ce que ceci ne mesure pas',
  'labour.notMeasuredWhy':
    'Ce sont les deux chiffres sur lesquels un atelier est habituellement jugé, et aucun des deux ne peut être produit honnêtement à partir de ce que ce système enregistre. Tous deux exigent une donnée que personne n’a jamais saisie ici.',
  'labour.noEfficiency':
    'Efficacité — heures produites rapportées aux heures disponibles. Il n’y a pas de planning, donc rien par quoi diviser.',
  'labour.noProductivity':
    'Productivité — heures facturées rapportées aux heures pointées. Il n’y a pas de pointeuse, donc rien par quoi diviser.',
  'labour.period':
    'Calculé sur les travaux facturés entre le {from} et le {to}. Les travaux en cours ne sont pas du chiffre d’affaires.',

  // --- Rappels de sécurité ---------------------------------------------------
  'recalls.title': 'Rappels de sécurité',
  'recalls.onRequest':
    'Ceci interroge l’autorité de sécurité routière : la recherche ne part que lorsque vous la demandez.',
  'recalls.check': 'Rechercher les rappels',
  'recalls.checkAgain': 'Rechercher à nouveau',
  'recalls.checking': 'Interrogation de l’autorité…',
  'recalls.caveat':
    'Voici les campagnes publiées pour une {make} {model} de {year}. Le registre est tenu par modèle et non par véhicule : il ne dit pas si celui-ci a déjà reçu l’intervention — seul le constructeur le sait.',
  'recalls.noneFound':
    'Aucune campagne n’est publiée pour ce modèle. Ce n’est pas la même chose que d’avoir fait vérifier ce véhicule.',
  'recalls.doNotDrive': 'Ne pas rouler',
  'recalls.parkOutside': 'Stationner dehors',
  'recalls.remedy': 'Intervention : {remedy}',

  // --- Clés d’accès ----------------------------------------------------------
  'passkey.useOne': 'Utiliser une clé d’accès',
  'passkey.needDealerGroup':
    'Saisissez d’abord votre groupe : c’est lui qui détermine dans quelle concession vous entrez.',
  'passkey.ceremonyFailed':
    'Votre appareil n’a pas pu terminer l’opération. Réessayez ou connectez-vous avec votre mot de passe.',

  'passkey.title': 'Clés d’accès',
  'passkey.lede':
    'Une clé d’accès vous connecte avec le téléphone ou l’ordinateur que vous déverrouillez déjà, à la place d’un mot de passe. Votre mot de passe reste valable et rien sur cet écran ne le supprime.',
  'passkey.addTitle': 'Ajouter une clé d’accès',
  'passkey.addNote':
    'Votre appareil vous demandera de confirmer. Rien de secret n’en sort — seulement une clé publique, sans intérêt pour qui en prendrait copie.',
  'passkey.label': 'Comment l’appeler',
  'passkey.labelPlaceholder': 'Ordinateur du bureau',
  'passkey.labelHint':
    'Ce nom s’affichera au moment de la supprimer : nommez l’appareil plutôt que vous-même.',
  'passkey.add': 'Ajouter',
  'passkey.adding': 'En attente de votre appareil…',
  'passkey.added': '{label} est enregistrée.',
  'passkey.unsupported':
    'Ce navigateur ne peut pas utiliser de clés d’accès. La plupart le peuvent, sur une connexion qui n’est pas en http simple.',

  'passkey.yoursTitle': 'Vos clés d’accès',
  'passkey.loading': 'Chargement de vos clés d’accès…',
  'passkey.none': 'Vous n’avez encore aucune clé d’accès.',
  'passkey.caption': {
    one: '{count} clé d’accès sur ce compte',
    other: '{count} clés d’accès sur ce compte',
  },
  'passkey.colLabel': 'Nom',
  'passkey.colAdded': 'Ajoutée',
  'passkey.colLastUsed': 'Dernière utilisation',
  'passkey.neverUsed': 'Jamais utilisée',
  'passkey.forget': 'L’oublier',
  'passkey.forgetTitle': 'Oublier {label} ?',
  'passkey.forgetConfirm': 'Cet appareil ne pourra plus vous connecter, et c’est irréversible.',
  'passkey.forgot': '{label} a été supprimée.',

  // --- Retrouver l’accès à son compte ---------------------------------------------
  'recover.link': 'J’ai oublié mon mot de passe',
  'recover.title': 'Retrouver l’accès',
  'recover.lede':
    'Choisissez comment prouver que ce compte est le vôtre. Quelle que soit la méthode, vous définissez ici même un nouveau mot de passe.',
  'recover.withAuthenticator': 'Utiliser mon application d’authentification',
  'recover.withAuthenticatorHint':
    'Pour toute personne ayant la connexion en deux étapes. Un code de l’application, ou l’un des codes de secours que vous avez conservés.',
  'recover.withCode': 'Utiliser un code de mon responsable',
  'recover.withCodeHint':
    'Demandez à un responsable de vous en délivrer un depuis l’écran Personnel. Il vous le lit ; il est valable quatre heures.',
  'recover.email': 'E-mail',
  'recover.codeFromApp': 'Code de votre application d’authentification',
  'recover.codeFromManager': 'Le code que votre responsable vous a donné',
  'recover.newPassword': 'Nouveau mot de passe',
  'recover.newPasswordAgain': 'Confirmez le nouveau mot de passe',
  'recover.mismatch': 'Ces deux mots de passe ne correspondent pas.',
  'recover.submit': 'Définir mon mot de passe',
  'recover.working': 'Enregistrement…',
  'recover.doneTitle': 'C’est fait',
  'recover.doneLede':
    'Votre mot de passe est modifié et tous les appareils connectés ont été déconnectés. Reconnectez-vous avec le nouveau.',
  'recover.toSignIn': 'Aller à la connexion',
  'recover.back': 'Choisir une autre méthode',
  'recover.noMethods':
    'Cette installation ne permet pas de récupérer un compte par elle-même. Demandez à un responsable de vous recréer un accès.',

  'staff.resetTitle': 'Réinitialiser son mot de passe',
  'staff.reset': 'Délivrer un code de réinitialisation',
  'staff.resetting': 'Délivrance…',
  'staff.resetNote':
    'Ceci lui permet de se reconnecter en tant que lui-même. Lisez-lui le code — il n’est affiché qu’une fois et vaut quatre heures.',
  'staff.resetWarning':
    'Vous transmettez la capacité de se connecter en tant que cette personne. Assurez-vous que c’est bien à elle que vous parlez.',
  'staff.resetIssued':
    'Un code de réinitialisation a été délivré {when} et n’a pas encore été utilisé.',
  'staff.resetDone': 'Je le lui ai lu',

  // The signal band on the deal desk: approvals somebody is blocking.
  'deals.awaitingTitle': 'En attente d’un responsable',
  'deals.awaitingNote': {
    one: '{count} vente n’est validée par personne et ne peut pas être livrée tant qu’elle ne l’est pas.',
    other: '{count} ventes ne sont validées par personne et ne peuvent pas être livrées tant qu’elles ne le sont pas.',
  },
  // Changing a value where it is written. See shared/InlineEdit.tsx.
  'inline.changeThis': '{label} : {value}. Appuyez pour le modifier.',
  'inline.saved': 'Enregistré',

  // --- Le carnet de l’atelier ---------------------------------------------------
  // Les voitures attendues, pas encore arrivées. « Rendez-vous », jamais
  // « créneau » : un atelier réserve une matinée, pas quarante minutes.
  'enum.appointmentStatus.Scheduled': 'Attendue',
  'enum.appointmentStatus.Arrived': 'Arrivée',
  'enum.appointmentStatus.NoShow': 'Non venue',
  'enum.appointmentStatus.Cancelled': 'Annulé',

  'diary.title': 'À venir',
  'diary.loading': 'Chargement du carnet…',
  'diary.empty': 'Rien de réservé. Le carnet est vide.',
  'diary.count': {
    one: '{count} voiture attendue',
    other: '{count} voitures attendues',
  },
  'diary.dayLoad': {
    one: '{count} voiture, {hours} h de travail',
    other: '{count} voitures, {hours} h de travail',
  },
  'diary.unestimated': 'Non estimé',
  'diary.colWhen': 'Quand',
  'diary.colCustomer': 'Client',
  'diary.colVehicle': 'Véhicule',
  'diary.colReason': 'Motif',
  'diary.dayLoadSome': {
    one: '{count} voiture, {hours} h réservées et {unestimated} non estimée',
    other: '{count} voitures, {hours} h réservées et {unestimated} non estimées',
  },
  // « Est. » se lirait comme le verbe « est » dans une en-tête de colonne.
  'diary.colHours': 'Estim.',
  'diary.colWhat': 'Et maintenant',
  'diary.itsHere': 'Elle est là',
  'diary.arriving': 'Ouverture de l’ordre…',
  'diary.didNotCome': 'Non venue',
  'diary.becameJob': 'Ordre {number}',

  'diary.book': 'Réserver une voiture',
  'diary.bookTitle': 'Réserver une voiture',
  'diary.customer': 'Client',
  'diary.vehicle': 'Voiture',
  'diary.when': 'Quand',
  'diary.hours': 'Heures de travail prévues',
  'diary.hoursHint': 'Laissez vide si personne ne l’a encore estimé.',
  'diary.reason': 'Motif de la visite',
  'diary.reasonPlaceholder': 'Révision annuelle',
  'diary.take': 'Réserver',
  'diary.taking': 'Réservation…',
  'diary.pickCustomer': 'Choisissez un client',
  'diary.pickVehicle': 'Choisissez une voiture',
  'diary.pickCustomerFirst': 'Choisissez d’abord le client — ses véhicules sont ensuite proposés.',

  // --- La console d’administration --------------------------------------------
  // Vocabulaire volontairement distinct de celui de la concession : qui lit ces
  // écrans exploite l’installation. « Concession » désigne ici un compte sur un
  // serveur, pas un lieu avec un parc d’exposition.
  'admin.badge': 'Administration',
  'admin.navDealerships': 'Concessions',
  'admin.navSupportAccess': 'Accès support',

  'admin.signInLede': 'Vous vous connectez à l’installation, pas à une concession.',
  'admin.signInCode': 'Code de votre application d’authentification',
  'admin.signInCodeNote': 'Ne le laissez vide que si vous n’en avez pas encore configuré.',

  'admin.secondFactorTitle': 'Configurez votre second facteur',
  'admin.secondFactorRequired':
    'Les comptes administrateur doivent en avoir un. Tant que ce n’est pas fait, cet écran est le seul que vous puissiez utiliser.',
  'admin.secondFactorIntro':
    'Ce compte peut entrer dans n’importe quelle concession de cette installation : un mot de passe seul ne suffit pas à le protéger.',
  'admin.noRecoveryCodes':
    'Il n’existe pas de codes de secours pour un compte administrateur. Si vous perdez ce téléphone, quelqu’un ayant accès à la base de données devra le réinitialiser pour vous.',

  'admin.dealerships': 'Concessions',
  'admin.setUpDealership': 'Créer une concession',
  'admin.suspendConfirm':
    'Tout le monde y est déconnecté immédiatement et ne peut plus travailler tant que l’accès n’est pas rétabli.',
  'admin.dealershipReady': '{name} est prête',
  'admin.firstManager':
    'Leur premier responsable est {email}. Lisez-lui ce code à voix haute — il définira lui-même son mot de passe avec, sur l’écran de connexion.',
  'admin.codeShownOnce': 'C’est la seule fois où ce code peut être affiché.',
  'admin.codeShownOnceWhy':
    'Seule une copie chiffrée est conservée : il est donc impossible de le retrouver. S’il s’égare, un nouveau code peut être délivré au responsable depuis l’écran Personnel de la concession elle-même. Les comptes sont ouverts, l’activité peut démarrer immédiatement.',
  'admin.passedItOn': 'Je l’ai transmis',
  'admin.loadingDealerships': 'Chargement de la liste des concessions…',
  'admin.noDealerships': 'Aucune concession sur cette installation. Créez la première ci-dessus.',
  'admin.dealershipsCaption': {
    one: '{count} concession sur cette installation',
    other: '{count} concessions sur cette installation',
  },
  'admin.colName': 'Nom',
  'admin.colKey': 'Clé',
  'admin.colStatus': 'Statut',
  'admin.colSchema': 'Schéma',
  'admin.colInService': 'En service',
  'admin.resume': 'Réactiver',
  'admin.suspend': 'Suspendre',
  'admin.suspendTitle': 'Suspendre {name} ?',

  'admin.setUpNote':
    'Ceci crée leur base de données, ouvre leur exercice pour le mois en cours et crée un responsable qui ajoutera ensuite tout le monde. Vous ne verrez ni ne choisirez jamais leur mot de passe.',
  'admin.dealershipName': 'Nom de la concession',
  'admin.shortName': 'Nom court',
  'admin.shortNameHint':
    'Lettres minuscules, chiffres et traits d’union. Leur personnel le saisit pour se connecter, et il ne peut plus être modifié ensuite.',
  'admin.firstLocation': 'Premier site',
  'admin.firstLocationPlaceholder': 'Site principal',
  'admin.locationCode': 'Code du site',
  'admin.managerName': 'Nom du responsable',
  'admin.managerEmail': 'E-mail du responsable',
  'admin.setItUp': 'Créer',
  'admin.settingItUp': 'Création…',
  'admin.notCreated': 'La concession n’a pas été créée.',

  'admin.supportAccess': 'Accès support',
  'admin.supportEnter': 'Entrer dans une concession',
  'admin.supportLede':
    'Vous pourrez lire leurs données sans rien modifier, pendant une heure au maximum. Cela apparaît dans leur propre journal, avec votre nom et le motif que vous indiquez ici.',
  'admin.supportDealership': 'Concession',
  'admin.supportReason': 'Pourquoi vous devez y entrer',
  'admin.supportReasonNote':
    'Ceci est enregistré définitivement, dans leur journal comme dans le nôtre. Écrivez ce que vous accepteriez de leur voir lire.',
  'admin.supportOpen': 'Ouvrir l’accès',
  'admin.supportOpening': 'Ouverture…',
  'admin.supportOpened':
    'Vous êtes dans {tenant} jusqu’à {time}. Ouvrez les écrans de la concession dans ce navigateur pour consulter ; fermez la visite ci-dessous lorsque vous avez terminé.',
  'admin.supportRecord': 'Le journal',
  'admin.supportLoading': 'Chargement du journal…',
  'admin.supportEmpty': 'Personne n’est encore entré dans une concession.',
  'admin.supportCaption': {
    one: '{count} visite de support',
    other: '{count} visites de support, de la plus récente à la plus ancienne',
  },
  'admin.supportColWho': 'Qui',
  'admin.supportColWhy': 'Motif',
  'admin.supportColOpened': 'Ouverte le',
  'admin.supportColState': 'État',
  'admin.supportCloseNow': 'Fermer maintenant',
  'admin.supportExpired': 'Expirée',
  'admin.supportClosed': 'Fermée le {date}',

  'nav.ageing': 'Qui nous doit',
  'nav.statements': 'Relevés',

  'ageing.title': 'Qui nous doit',
  'ageing.loading': 'Calcul de qui doit quoi…',
  'ageing.denied': 'Vous n’avez pas accès à ceci.',
  'ageing.failed': 'Impossible de charger le rapport d’ancienneté des créances.',
  'ageing.empty': 'Personne ne nous doit rien pour le moment.',
  'ageing.colCustomer': 'Client',
  'ageing.colCurrent': 'Courant',
  'ageing.col31to60': '31 à 60 jours',
  'ageing.col61to90': '61 à 90 jours',
  'ageing.colOver90': 'Plus de 90 jours',
  'ageing.colTotal': 'Total',
  'ageing.totals': 'Total',

  'statement.title': 'Relevé de compte client',
  'statement.customer': 'Client',
  'statement.from': 'Du',
  'statement.to': 'Au',
  'statement.pickCustomer': 'Choisissez un client pour voir son relevé.',
  'statement.loading': 'Calcul du relevé…',
  'statement.denied': 'Vous n’avez pas accès à ceci.',
  'statement.failed': 'Impossible de charger le relevé.',
  'statement.opening': 'Solde reporté',
  'statement.closing': 'Solde dû',
  'statement.colDate': 'Date',
  'statement.colReference': 'Référence',
  'statement.colAmount': 'Montant',
  'statement.colBalance': 'Solde',
  'statement.kindInvoice': 'Facturé',
  'statement.kindPayment': 'Payé',
  'statement.empty': 'Rien ne s’est passé durant cette période.',

  'customers.creditLimit': 'Limite de crédit',
  'customers.creditLimitNone': 'Aucune limite définie',
  'customers.creditLimitEdit': 'Modifier',
  'customers.creditLimitPlaceholder': 'Aucune limite',
  'customers.creditLimitSave': 'Enregistrer',
  'customers.creditLimitSaving': 'Enregistrement…',
};
