// en — the English catalogue, and the schema every other language is checked
// against.
//
// Use:  t('nav.stock'). Keys are `area.thing`, lower case, dot separated.
// Edit: adding a key here makes `npm run typecheck` fail for fr, de, ru and ar
//       until each has one too. That is the point — a missing translation is a
//       build failure rather than an English sentence surfacing mid-screen.
//
//       Write the ENGLISH first and write it well. Every other catalogue is
//       translated from this text, so a vague or clipped phrase here becomes
//       four vague or clipped phrases. Prefer a whole sentence over a fragment
//       assembled at the call site: "{n} of {total}" cannot be reordered by a
//       translator, and German and Arabic both need to reorder it.
//
//       A plural entry is an object, never an `if (n === 1)` at the call site.
//       Russian needs four forms and Arabic six.

export const en = {
  // --- Words the whole application uses -------------------------------------
  'app.name': 'DealerFOSS',
  'common.loading': 'Loading…',
  'common.save': 'Save',
  'common.saving': 'Saving…',
  'common.cancel': 'Cancel',
  'common.close': 'Close',
  'common.retry': 'Try again',
  'common.search': 'Search',
  'common.searching': 'Searching…',
  'common.none': 'None',
  'common.all': 'All',
  'common.yes': 'Yes',
  'common.no': 'No',
  'common.back': 'Back',
  'common.continue': 'Continue',
  'common.unexpected': 'Something went wrong. Try again.',
  'common.notPermitted': 'You do not have permission to see this.',
  'common.unreachable': 'Could not reach the server. Is it running?',

  // --- Refusals the reader sees on any screen -------------------------------
  // See apiMessage.ts for why these are translated and the rest are not.
  'error.network': 'Could not reach the server. Is it running?',
  'error.invalidCredentials': 'That email address and password do not match an account.',
  'error.sessionRequired': 'Sign in to use this.',
  'error.adminSessionRequired':
    'Sign in as an administrator to use the control plane. A dealership sign-in does not reach it.',
  'error.sessionInvalid': 'Your session has ended. Sign in again.',
  'error.antiForgeryFailed':
    'This browser no longer holds a token matching its session. Sign in again.',
  'error.secondFactorRejected': 'That code was not accepted.',
  'error.secondFactorRequired':
    'Your role requires two-step sign-in. Set it up to reach the rest of the application.',
  'error.mfaNotEnrolled': 'Two-step sign-in is not set up on this account.',
  'error.mfaAlreadyOn': 'Two-step sign-in is already on for this account.',
  'error.notATenantCaller': 'An administrator cannot act as a dealership user.',
  'error.tenantRequired': 'Say which dealer group this is.',
  'error.tenantNotFound': 'No active dealer group matches that name.',

  // --- The shell ------------------------------------------------------------
  'shell.skipToContent': 'Skip to content',
  'shell.mainNavigation': 'Main',
  'shell.signOut': 'Sign out',
  'shell.shortcuts': 'Shortcuts',
  'shell.shortcutsTitle': 'Keyboard shortcuts ( ? )',
  'shell.language': 'Language',
  'shell.appearance': 'Appearance',

  'nav.dashboard': 'This month',
  'nav.customers': 'Customers',
  'nav.leads': 'Enquiries',
  'nav.deals': 'Deals',
  'nav.stock': 'Stock',
  'nav.workshop': 'Workshop',
  'nav.parts': 'Parts',
  'nav.trialBalance': 'Trial balance',
  'nav.books': 'The books',
  'nav.records': 'Records',
  'nav.staff': 'People',
  'nav.secondFactor': 'Two-step sign-in',

  // --- Appearance -----------------------------------------------------------
  'appearance.auto': 'Auto',
  'appearance.autoHint': 'Follow this machine',
  'appearance.light': 'Light',
  'appearance.lightHint': 'Always light',
  'appearance.dark': 'Dark',
  'appearance.darkHint': 'Always dark',

  // --- Signing in -----------------------------------------------------------
  'signIn.lede': 'Sign in to your dealership.',
  'signIn.dealerGroup': 'Dealer group',
  'signIn.email': 'Email',
  'signIn.password': 'Password',
  'signIn.submit': 'Sign in',
  'signIn.submitting': 'Signing in…',
  'signIn.codeLede':
    'Enter the six-digit code from your authenticator app, or one of your recovery codes.',
  'signIn.code': 'Code',
  'signIn.checking': 'Checking…',
  'signIn.startAgain': 'Start again',

  // --- Setting a first password ---------------------------------------------
  'setPassword.title': 'Set your password',
  'setPassword.lede':
    'Your manager gave you a code. Use it once here to choose a password only you know — nobody at the dealership can see what you pick.',
  'setPassword.dealership': 'Dealership',
  'setPassword.email': 'Email',
  'setPassword.code': 'Code',
  'setPassword.password': 'New password',
  'setPassword.passwordHint': 'At least 12 characters. Length is what makes one hard to guess.',
  'setPassword.again': 'New password again',
  'setPassword.mismatch': 'Those two do not match.',
  'setPassword.failed': 'That did not work.',
  'setPassword.submit': 'Set my password',
  'setPassword.doneTitle': 'You are set up',
  'setPassword.doneLede': 'Sign in with your email address and the password you just chose.',
  'setPassword.toSignIn': 'Go to sign in',

  // --- Two-step sign-in -----------------------------------------------------
  'secondFactor.title': 'Two-step sign-in',
  'secondFactor.required':
    'Your dealership requires two-step sign-in for your role. Until you set it up, this is the only screen you can use.',
  'secondFactor.intro':
    'After this, signing in asks for a six-digit code from an app on your phone as well as your password. Google Authenticator, Authy and 1Password all work.',
  'secondFactor.start': 'Start',
  'secondFactor.starting': 'Starting…',
  'secondFactor.pointApp': 'Point your authenticator app at this square.',
  'secondFactor.qrTitle': 'Scan this with your authenticator app',
  'secondFactor.cannotScan': 'Can’t scan it?',
  'secondFactor.typeInstead': 'Type this into the app by hand instead:',
  'secondFactor.enterCode': 'Now enter the code it shows',
  'secondFactor.turnOn': 'Turn it on',
  'secondFactor.checking': 'Checking…',
  'secondFactor.notYet':
    'Nothing has changed about signing in yet. It only takes effect once the code above is accepted.',
  'secondFactor.onNow':
    'Two-step sign-in is on. From now on you will be asked for a code after your password.',
  'secondFactor.saveTitle': 'Save these somewhere safe',
  'secondFactor.saveLede':
    'Each of these works once, and only if you lose your phone. This is the only time they will ever be shown.',
  'secondFactor.recoveryCodes': 'Recovery codes',

  // --- Keyboard shortcuts ---------------------------------------------------
  'shortcuts.title': 'Keyboard shortcuts',
  'shortcuts.space': 'Space',
  'shortcuts.note':
    'Shortcuts are ignored while you are typing in a field, so they never eat a character you meant to write.',
  'shortcuts.goDashboard': 'Go to the dashboard',
  'shortcuts.goStock': 'Go to stock',
  'shortcuts.goCustomers': 'Go to customers',
  'shortcuts.goLeads': 'Go to enquiries',
  'shortcuts.goDeals': 'Go to deals',
  'shortcuts.goWorkshop': 'Go to the workshop',
  'shortcuts.goParts': 'Go to parts',
  'shortcuts.goBooks': 'Go to the books',
  'shortcuts.monthBefore': 'On the dashboard: the month before',
  'shortcuts.monthAfter': 'On the dashboard: the month after',
  'shortcuts.thisMonth': 'On the dashboard: back to this month',
  'shortcuts.showList': 'Show this list',

  // --- Words the API sends back ---------------------------------------------
  // The server answers with `"OnHold"`, and a screen that printed that verbatim
  // would say "OnHold" in every language including English. These are the only
  // place an API enum is turned into something a person reads; see
  // shared/i18n/enums.ts for the lookup that guarantees no value is missed.
  'enum.inventoryStatus.Incoming': 'Incoming',
  'enum.inventoryStatus.Reconditioning': 'Reconditioning',
  'enum.inventoryStatus.Available': 'Available',
  'enum.inventoryStatus.OnHold': 'On hold',
  'enum.inventoryStatus.Sold': 'Sold',
  'enum.inventoryStatus.Removed': 'Removed',

  'enum.leadStatus.New': 'New',
  'enum.leadStatus.Working': 'Working',
  'enum.leadStatus.Appointment': 'Appointment',
  'enum.leadStatus.Won': 'Won',
  'enum.leadStatus.Lost': 'Lost',

  'enum.leadSource.WalkIn': 'Walk-in',
  'enum.leadSource.Phone': 'Phone',
  'enum.leadSource.Website': 'Website',
  'enum.leadSource.Referral': 'Referral',
  'enum.leadSource.Marketplace': 'Marketplace listing',
  'enum.leadSource.Unknown': 'Not recorded',

  'enum.dealStatus.Draft': 'Draft',
  'enum.dealStatus.Submitted': 'Submitted',
  'enum.dealStatus.Approved': 'Approved',
  'enum.dealStatus.Delivered': 'Delivered',
  'enum.dealStatus.Lost': 'Lost',

  'enum.chargeKind.VehiclePrice': 'Vehicle price',
  'enum.chargeKind.Fee': 'Fee',
  'enum.chargeKind.Discount': 'Discount',
  'enum.chargeKind.Accessory': 'Accessory',

  'enum.repairOrderStatus.Booked': 'Booked',
  'enum.repairOrderStatus.InProgress': 'In progress',
  'enum.repairOrderStatus.Completed': 'Completed',
  'enum.repairOrderStatus.Invoiced': 'Invoiced',
  'enum.repairOrderStatus.Cancelled': 'Cancelled',

  'enum.serviceLineKind.Labour': 'Labour',
  'enum.serviceLineKind.Part': 'Part',
  'enum.serviceLineKind.Sublet': 'Sent out',

  'enum.financeProductKind.Warranty': 'Warranty',
  'enum.financeProductKind.Gap': 'GAP',
  'enum.financeProductKind.ServicePlan': 'Service plan',
  'enum.financeProductKind.Protection': 'Protection',
  'enum.financeProductKind.Other': 'Other',

  'enum.periodState.Open': 'Open',
  'enum.periodState.Closed': 'Closed',

  'enum.booksState.NotOpened': 'Not opened',
  'enum.booksState.Open': 'Open',
  'enum.booksState.Closed': 'Closed',
  'enum.booksState.Unknown': 'Unknown',

  'enum.importKind.Customers': 'Customers',
  'enum.importKind.Vehicles': 'Vehicles',

  'enum.tenantStatus.Active': 'Active',
  'enum.tenantStatus.Suspended': 'Suspended',
  'enum.tenantStatus.Provisioning': 'Provisioning',
  'enum.tenantStatus.Archived': 'Archived',

  // --- Stock ----------------------------------------------------------------
  'stock.title': 'Stock',
  'stock.status': 'Status',
  'stock.loading': 'Loading the stock list…',
  'stock.denied':
    'You do not have access to this location’s stock. Ask a manager if you think that is wrong.',
  'stock.failed': 'Could not load the stock list.',
  'stock.empty': 'Nothing here yet. Cars appear once they are taken into stock.',
  'stock.onlyStockNumber': 'Showing stock number {stock} only.',
  'stock.showEverything': 'Show everything',
  'stock.colStock': 'Stock',
  'stock.colVehicle': 'Vehicle',
  'stock.colVin': 'VIN',
  'stock.colStatus': 'Status',
  'stock.count': { one: '{count} car in stock', other: '{count} cars in stock' },
  'stock.countCapped': 'The first {count} cars in stock. There may be more.',
  'stock.cappedNote':
    'Showing the first {count}. There may be more — narrow it with the status filter until paging exists.',

  'enum.accountKind.Asset': 'Asset',
  'enum.accountKind.Liability': 'Liability',
  'enum.accountKind.Equity': 'Equity',
  'enum.accountKind.Revenue': 'Revenue',
  'enum.accountKind.Expense': 'Expense',

  // --- Trial balance --------------------------------------------------------
  'trialBalance.title': 'Trial balance',
  'trialBalance.loading': 'Adding it up…',
  'trialBalance.denied': 'You do not have access to these figures.',
  'trialBalance.failed': 'Could not load the balances.',
  'trialBalance.empty': 'Nothing posted yet. Entries appear here once a car has been delivered.',
  'trialBalance.inBalance': 'In balance — debits and credits both come to {total}.',
  'trialBalance.outOfBalance':
    'Out of balance by {difference}. Something was lost on the way in.',
  'trialBalance.colCode': 'Code',
  'trialBalance.colAccount': 'Account',
  'trialBalance.colKind': 'Kind',
  'trialBalance.colDebits': 'Debits',
  'trialBalance.colCredits': 'Credits',
  'trialBalance.colBalance': 'Balance',
  'trialBalance.total': 'Total',

  // --- Customers ------------------------------------------------------------
  'enum.customerKind.Person': 'Person',
  'enum.customerKind.Business': 'Business',

  'customers.title': 'Customers',
  'customers.find': 'Find someone',
  'customers.findPlaceholder': 'Name, phone, or email',
  'customers.add': 'Add a customer',
  'customers.looking': 'Looking…',
  'customers.denied':
    'You do not have access to customer records. Ask a manager if you think that is wrong.',
  'customers.failed': 'Could not load customers.',
  'customers.noMatches': 'Nobody matches that.',
  'customers.colName': 'Name',
  'customers.colKind': 'Kind',
  'customers.colEmail': 'Email',
  'customers.colPhone': 'Phone',
  'customers.count': { one: '{count} customer', other: '{count} customers' },
  'customers.countCapped': 'The first {count} customers. There may be more.',
  'customers.cappedNote':
    'Showing the first {count}. There may be more — narrow the search until paging exists.',

  'customers.kindLabel': 'Person or business',
  'customers.firstName': 'First name',
  'customers.lastName': 'Last name',
  'customers.businessName': 'Business name',
  'customers.email': 'Email',
  'customers.phone': 'Phone',
  'customers.submit': 'Add',
  'customers.checking': 'Checking for duplicates…',
  'customers.adding': 'Adding…',

  'customers.duplicateTitle': 'Somebody like this is already here',
  'customers.duplicateLede':
    'Adding a second record for the same person splits their history — their service, their deals, and their contact details stop agreeing. Check whether one of these is them.',
  'customers.noContactDetails': 'no contact details',
  'customers.oneOfTheseIsThem': 'One of these is them',
  'customers.addAnyway': 'None of these — add anyway',

  // --- Bringing records in and taking them out ------------------------------
  'enum.importOutcome.Pending': 'Pending',
  'enum.importOutcome.Created': 'Added',
  'enum.importOutcome.Updated': 'Already here',
  'enum.importOutcome.Skipped': 'Skipped',
  'enum.importOutcome.Failed': 'Refused',

  'records.title': 'Records',
  'records.bringIn': 'Bring records in',
  'records.bringInLede':
    'A spreadsheet exported from your old system, saved as CSV. Nothing is written until you have run it as a practice first.',
  'records.whatIsInIt': 'What is in the file',
  'records.file': 'The file',
  'records.unreadableFile': 'That file could not be read. Is it a text CSV?',
  'records.practice': 'Practice run',
  'records.practising': 'Trying it…',
  'records.importForReal': 'Import for real',
  'records.importing': 'Importing…',
  'records.practiseFirst':
    'Run the practice first. It changes nothing and tells you exactly what the real one will do.',

  'records.takeOut': 'Take records out',
  'records.takeOutLede':
    'Downloads everything of that kind as a CSV. It is the same shape this page accepts back, so you can move it anywhere — including into another system entirely.',
  'records.downloadCustomers': 'Download customers',
  'records.downloadVehicles': 'Download vehicles',

  'records.couldNotRun': 'That import could not be run.',
  'records.whatWouldHappen': 'What would happen',
  'records.whatHappened': 'What happened',
  // One sentence rather than five fragments joined at the call site: German and
  // Arabic both need to reorder it, and a comma-separated list assembled in
  // English word order cannot be reordered by a translator.
  'records.summaryPractice': {
    one: 'Of {count} row: {created} would be added, {updated} already here, {skipped} skipped, {failed} could not be read.',
    other:
      'Of {count} rows: {created} would be added, {updated} already here, {skipped} skipped, {failed} could not be read.',
  },
  'records.summaryReal': {
    one: 'Of {count} row: {created} added, {updated} already here, {skipped} skipped, {failed} refused.',
    other:
      'Of {count} rows: {created} added, {updated} already here, {skipped} skipped, {failed} refused.',
  },
  'records.nothingWritten': 'Nothing has been written. This was a practice run.',
  'records.rowsToLookAt': 'Rows to look at',
  'records.rowsToLookAtLede':
    'The line number is the one you see in your spreadsheet, and the row is quoted exactly as it arrived. Fix the file and run it again — nothing here edits what you sent.',
  'records.problemCount': {
    one: '{count} row needing attention',
    other: '{count} rows needing attention',
  },
  'records.colLine': 'Line',
  'records.colWhatHappened': 'What happened',
  'records.colTheRow': 'The row',
  'records.timeout':
    'That import is taking longer than expected. It is still running — this page just stopped waiting.',

  // --- The books ------------------------------------------------------------
  'periods.title': 'The books',
  'periods.loading': 'Loading the books…',
  'periods.denied': 'You do not have access to the accounts.',
  'periods.failed': 'The books could not be read.',
  'periods.actionFailed': 'That did not work.',
  'periods.lede':
    'Nothing can be posted into a month until its books are open, and nothing can be posted into one that has been closed. Closing is something you do when the month-end work is finished — there is no date that does it for you.',
  'periods.none': 'No months are open yet. Nothing can be posted until you open one.',

  'periods.openAMonth': 'Open a month',
  'periods.openLede':
    'Until a month is open, nothing dated in it can be posted — a sale or a service invoice will be refused. Opening it is deliberate so the books have a start you chose rather than one inferred from the first thing anybody typed.',
  'periods.year': 'Year',
  'periods.month': 'Month',
  'periods.openIt': 'Open it',

  'periods.reopenTitle': 'Reopen {month}?',
  'periods.reopenLede':
    'This month has been closed, and its figures may already have been reported. Reopening it is recorded against the month with your reason, so anybody looking later can see what happened and why.',
  'periods.reopenWhy': 'Why is it being reopened?',
  'periods.reopenPlaceholder': 'A supplier invoice arrived on the 4th',
  'periods.reopenIt': 'Reopen it',
  'periods.leaveClosed': 'Leave it closed',
  'periods.reopen': 'Reopen',

  'periods.closeIt': 'Close it',
  'periods.confirmClose':
    'Close {month}? Nothing more can be posted into it until it is reopened.',

  'periods.caption': 'Every month of the books, newest first.',
  'periods.colMonth': 'Month',
  'periods.colCutoff': 'Cutoff',
  'periods.colEntries': 'Entries',
  'periods.colState': 'State',

  'periods.historyTitle': 'What has happened to the books',
  'periods.wasOpened': '{month} opened',
  'periods.wasClosed': '{month} closed',
  'periods.wasReopened': '{month} reopened',

  // --- Enquiries ------------------------------------------------------------
  'leads.title': 'Enquiries',
  'leads.show': 'Show',
  'leads.stillChasing': 'Still being chased',
  'leads.everything': 'Everything',
  'leads.onlyMine': 'Only mine',
  'leads.take': 'Take an enquiry',
  'leads.loading': 'Loading the enquiries…',
  'leads.denied':
    'You do not have access to enquiries at this location. Ask a manager if you think that is wrong.',
  'leads.failed': 'Could not load the enquiries.',
  'leads.openFailed': 'Could not open that enquiry.',
  'leads.empty':
    'No enquiries here. One starts the moment somebody rings up or walks onto the lot.',

  'leads.colCustomer': 'Customer',
  'leads.colAskedAbout': 'Asked about',
  'leads.colCameFrom': 'Came from',
  'leads.colDays': 'Days',
  'leads.colChasedBy': 'Chased by',
  'leads.colStage': 'Stage',
  'leads.nothingSpecific': 'Nothing specific',
  'leads.nobodyYet': 'Nobody yet',
  'leads.you': 'You',
  'leads.somebodyElse': 'Somebody else',
  'leads.count': { one: '{count} enquiry', other: '{count} enquiries' },
  'leads.countCapped': 'The first {count} enquiries. There may be more.',
  'leads.cappedNote':
    'Showing the first {count}. There may be more — narrow it with the filters until paging exists.',

  'leads.cameIn': 'came in {date}',
  'leads.unclaimed': 'Nobody has picked this up yet.',
  'leads.yoursToChase': 'You are chasing this one.',
  'leads.theirsToChase': '{name} is chasing this one.',
  'leads.putBack': 'Put it back in the pool',
  'leads.iWillChase': 'I will chase this',
  'leads.takeItOver': 'Take it over',
  'leads.handTo': 'Hand to',
  'leads.chooseColleague': 'Choose a colleague',
  'leads.buildTheDeal': 'Build the deal',
  'leads.whatHappened': 'What happened',
  'leads.finished':
    'This enquiry is finished. A customer who comes back later starts a new one.',
  'leads.note': 'Note (goes on the record)',
  'leads.notePlaceholder': 'Left a voicemail · coming in Saturday · bought elsewhere',
  'leads.reopenedHere':
    'A lost enquiry that comes back is reopened here rather than retyped, so the first attempt stays part of the story.',

  // The move as a person would say it. `reopen` is the same transition as
  // `startChasing` seen from a lost enquiry, and reads differently on purpose.
  'leads.moveReopen': 'Reopen it',
  'leads.moveStartChasing': 'Start chasing',
  'leads.moveAppointment': 'They are coming in',
  'leads.moveWon': 'They are buying',
  'leads.moveLost': 'Mark it lost',

  'leads.captureTitle': 'Take an enquiry',
  'leads.findCustomer': 'Find the customer',
  'leads.whoIsAsking': 'Who is asking',
  'leads.chooseSomebody': 'Choose somebody…',
  'leads.searchAboveNote':
    'Search above to find them. An enquiry has to belong to somebody, so add them on the customers page first if they are new.',
  'leads.whichLocation': 'Which location',
  'leads.chooseLocation': 'Choose a location…',
  'leads.onlyLocation':
    'This enquiry belongs to {name} ({code}), the only location you work at.',
  'leads.howTheyReachedUs': 'How they reached us',
  'leads.carAskedAbout': 'Car they asked about (optional)',
  'leads.whatTheySaid': 'What they said',
  'leads.whatTheySaidPlaceholder': 'Budget, trade-in, when they need it by…',
  'leads.save': 'Save the enquiry',
  'leads.locationsFailed': 'Could not load your locations.',
  'leads.lookupFailed': 'Could not look that up.',
  'leads.saveFailed': 'That enquiry could not be saved.',

  // --- The deal desk --------------------------------------------------------
  'deals.title': 'Deals',
  'deals.show': 'Show',
  'deals.stillWorked': 'Still being worked',
  'deals.everything': 'Everything',
  'deals.start': 'Start a deal',
  'deals.loading': 'Loading the deals…',
  'deals.denied':
    'You do not have access to deals at this location. Ask a manager if you think that is wrong.',
  'deals.failed': 'Could not load the deals.',
  'deals.openFailed': 'Could not open that deal.',
  'deals.empty': 'No deals here. One starts when a car is priced for somebody.',
  'deals.documentFailed': 'The document could not be opened.',

  'deals.colCustomer': 'Customer',
  'deals.colVehicle': 'Vehicle',
  'deals.colStock': 'Stock',
  'deals.colDue': 'Due',
  'deals.colStage': 'Stage',
  'deals.count': { one: '{count} deal', other: '{count} deals' },
  'deals.countCapped': 'The first {count} deals. There may be more.',
  'deals.cappedNote':
    'Showing the first {count}. There may be more — narrow it with the filter until paging exists.',

  'deals.printOrder': 'Print the order',
  'deals.stockLine': 'Stock {stock}',
  'deals.numbersCaption': 'The numbers on this deal',
  'deals.colLine': 'Line',
  'deals.colDescription': 'Description',
  'deals.colAmount': 'Amount',
  'deals.lineProduct': 'Product',
  'deals.lineTradeIn': 'Trade-in',
  'deals.owesMore': 'owes more than it is worth',
  'deals.dueFromCustomer': 'Due from the customer',
  'deals.frozen':
    'The numbers are frozen. They stopped being editable when this deal was submitted, so what a manager approves is what was put in front of them.',
  'deals.whatHappened': 'What happened',

  'deals.finished': 'This deal is finished. Nothing more can happen to it.',
  'deals.sendToManager': 'Send to a manager',
  'deals.approve': 'Approve',
  'deals.handOver': 'Hand the car over',
  'deals.markLost': 'Mark lost',
  'deals.markedLostNote': 'Marked lost from the deal desk.',
  'deals.cannotApproveOwn':
    'Whoever built this deal cannot be the one who approves it. If that is you, a manager has to do it.',

  'deals.soldWithTheCar': 'Sold with the car',
  'deals.colProduct': 'Product',
  'deals.colPrice': 'Price',
  'deals.colGross': 'Gross',
  'deals.productGross': '{amount} made on what was sold with the car.',

  // --- Deal terms (the charges editor) --------------------------------------
  'terms.title': 'The numbers',
  'terms.caption': 'The charges on this deal',
  'terms.colLine': 'Line',
  'terms.colDescription': 'Description',
  'terms.colAmount': 'Amount',
  'terms.remove': 'Remove',
  'terms.lineKind': 'Line {n} kind',
  'terms.lineDescription': 'Line {n} description',
  'terms.lineAmount': 'Line {n} amount',
  'terms.addLine': 'Add a line',
  'terms.addTradeIn': 'Add a trade-in',
  'terms.dropTradeIn': 'No trade-in after all',
  'terms.tradeInTitle': 'The trade-in',
  'terms.whatTheyTrade': 'What they are trading',
  'terms.whatWeAllow': 'What we are allowing for it',
  'terms.whatIsOwed': 'What is still owed on it',
  'terms.negativeEquity':
    'They owe more on it than we are allowing, so the difference is added to this deal.',
  'terms.save': 'Save the numbers',
  'terms.rejected': 'Those numbers were not accepted.',
  'terms.needsPrice': 'Every deal needs a price for the car itself before it can be saved.',

  // --- F&I products ---------------------------------------------------------
  'products.title': 'Sold with the car',
  'products.loading': 'Loading what can be sold…',
  'products.none': 'No products are set up to sell. A manager adds them under the finance catalogue.',
  'products.lede':
    'The prices start from the catalogue and are yours to change — what you type here is what gets recorded on this deal, and later price-list changes will not touch it.',
  'products.colSell': 'Sell',
  'products.colProduct': 'Product',
  'products.colPrice': 'Price',
  'products.colCost': 'Cost',
  'products.colGross': 'Gross',
  'products.sellThis': 'Sell {product}',
  'products.priceFor': 'Price for {product}',
  'products.costOf': 'Cost of {product}',
  'products.termMonths': { one: '{count} month', other: '{count} months' },
  'products.withdrawn': 'no longer offered',
  'products.nothingSelected': 'Nothing selected.',
  'products.addedToDeal': '{added} added to the deal, making {gross}.',
  'products.saveFailed': 'That did not save.',
  'products.saveWhatIsSold': 'Save what is being sold',

  // --- Starting a deal ------------------------------------------------------
  'startDeal.title': 'Start a deal',
  'startDeal.findBuyer': 'Find the buyer',
  'startDeal.buyer': 'Who is buying',
  'startDeal.chooseBuyer': 'Choose somebody…',
  'startDeal.searchAbove':
    'Search above to find them. Add them on the customers page if they are new.',
  'startDeal.fromEnquiry': 'From the enquiry for {name}. The deal will be linked to it.',
  'startDeal.thatCustomer': 'that customer',
  'startDeal.whichCar': 'Which car',
  'startDeal.chooseCar': 'Choose a car…',
  'startDeal.nothingAvailable':
    'Nothing on the lot is available right now. A car already on another deal is held until that deal ends.',
  'startDeal.chooseCarFirst': 'Choose a car first.',
  'startDeal.submit': 'Start the deal',
  'startDeal.starting': 'Starting…',
  'startDeal.failed': 'That deal could not be started.',
  'startDeal.stockFailed': 'Could not load the stock list.',
  'startDeal.customerFailed': 'Could not read that customer.',

  // --- People ---------------------------------------------------------------
  'staff.title': 'People',
  'staff.loading': 'Loading the people who work here…',
  'staff.denied': 'You do not have access to the staff list. Ask a manager if you need it.',
  'staff.failed': 'The staff list could not be read.',
  'staff.actionFailed': 'That did not work.',
  'staff.empty': 'Nobody here yet.',
  'staff.add': 'Add somebody',
  'staff.caption': 'Everybody whose access reaches a location you work at.',
  'staff.colName': 'Name',
  'staff.colEmail': 'Email',
  'staff.colHolds': 'Holds',
  'staff.colSecondFactor': 'Second factor',
  'staff.colState': 'State',
  'staff.holdsNothing': 'Nothing yet',

  'staff.stateStopped': 'Stopped',
  'staff.stateAwaiting': 'Awaiting first password',
  'staff.stateWorking': 'Working',

  'staff.codeFor': 'Code for {name}',
  'staff.readItOut':
    'Read this out to them. They set their own password with it at the sign-in screen — nobody else ever types it, including you.',
  'staff.onlyTimeShown': 'This is the only time it can be shown.',
  'staff.onlyTimeShownRest':
    'Only a hash of it is stored, so it cannot be looked up again — if it goes astray, issue a new one, which stops this one working. It expires {expires}.',
  'staff.passedItOn': 'I have passed it on',

  'staff.addTitle': 'Add somebody',
  'staff.addLede':
    'They will not be able to sign in until they set a password with the code this produces. You never see or choose their password.',
  'staff.name': 'Name',
  'staff.email': 'Email',
  'staff.addAndMakeCode': 'Add and make a code',

  'staff.hasSecondFactor': 'has a second factor',
  'staff.noSecondFactor': 'no second factor',
  'staff.whatTheyHold': 'What they hold',
  'staff.holdsNothingYet': 'Nothing yet, so they can sign in and see nothing.',
  'staff.everywhere': 'everywhere',
  'staff.oneLocation': 'one location',
  'staff.takeItAway': 'Take it away',
  'staff.giveARole': 'Give them a role',
  'staff.role': 'Role',
  'staff.chooseRole': 'Choose a role',
  'staff.holdingGrants': 'Holding it grants: {permissions}',
  'staff.andObligesSecondFactor': ' — and obliges them to set up a second factor.',
  'staff.where': 'Where',
  'staff.everywhereInOrg': 'Everywhere in the organization',
  'staff.giveThem': 'Give them this',
  'staff.makeNewCode': 'Make a new code',
  'staff.stopAccount': 'Stop this account',
  'staff.letThemBackIn': 'Let them back in',
  'staff.stoppingNote':
    'Stopping an account ends their sessions on the very next request, and deletes nothing — their name still has to appear against the work they did.',

  // --- Parts ----------------------------------------------------------------
  'parts.title': 'Parts',
  'parts.loading': 'Loading the parts catalogue…',
  'parts.denied':
    'You do not have access to parts at this location. Ask a manager if you think that is wrong.',
  'parts.failed': 'The catalogue could not be read.',
  'parts.actionFailed': 'That did not work.',
  'parts.add': 'Add a part',
  'parts.find': 'Find a part',
  'parts.findPlaceholder': 'Number or description',
  'parts.findHint':
    'The number is matched however it was typed — MZ-690411, mz690411, and MZ 690 411 all find the same part.',
  'parts.catalogueEmpty': 'Nothing in the catalogue yet.',
  'parts.noMatches': 'Nothing matches that.',
  'parts.caption': 'Parts, with what is on the shelf at the locations you cover.',
  'parts.colNumber': 'Number',
  'parts.colDescription': 'Description',
  'parts.colWhere': 'Where',
  'parts.colOnHand': 'On hand',
  'parts.colCostEach': 'Cost each',
  'parts.notStocked': 'Not stocked',
  'parts.oneLocation': 'one location',
  'parts.noneOnHand': 'None',

  'parts.costingTitle': 'How parts are costed',
  'parts.costingMethod': 'Method',
  'parts.costingFutureOnly': 'future sales only',
  'parts.costingNote':
    'Work already invoiced keeps the cost it was sold at — changing this cannot restate a month you have already reported on.',
  'parts.costingApplies': 'This applies to {futureOnly}. {rest}',

  'parts.addTitle': 'Add a part',
  'parts.addLede':
    'A part number means the same component at every location, so this is a group-level change. The stock itself belongs to whichever shelf it is booked onto.',
  'parts.partNumber': 'Part number',
  'parts.description': 'Description',
  'parts.addIt': 'Add it',

  'parts.noneVisible': 'None of this on any shelf you can see.',
  'parts.shelfHeading': '{code} — {quantity} on hand at {cost} each',
  'parts.deliveriesCaption': 'Deliveries of {part} at {code}.',
  'parts.colReceived': 'Received',
  'parts.colNote': 'Note',
  'parts.colCameIn': 'Came in',
  'parts.colLeft': 'Left',

  'parts.bookIn': 'Book a delivery in',
  'parts.ontoWhichShelf': 'Onto which shelf',
  'parts.howMany': 'How many',
  'parts.costEach': 'Cost each',
  'parts.deliveryNote': 'Delivery note',
  'parts.bookItIn': 'Book it in',

  // --- This month (the dashboard) -------------------------------------------
  'dash.soFar': 'So far this month.',
  'dash.asFinished': 'The month as it finished.',
  'dash.whichMonth': 'Which month',
  'dash.previousMonth': 'Previous month',
  'dash.nextMonth': 'Next month',
  'dash.previousMonthTitle': 'Previous month ( [ )',
  'dash.nextMonthTitle': 'Next month ( ] )',
  'dash.rooftop': 'Rooftop',
  'dash.everywhere': 'Everywhere I can see',
  'dash.loading': 'Adding the month up…',
  'dash.denied': 'You do not have access to any of the figures on this dashboard.',
  'dash.failed': 'Could not load the month.',

  'dash.withheldTrading':
    'The money on this month is not yours to see, so the figures below are only the stock.',
  'dash.withheldStock': 'Stock is not yours to see, so this month shows only what was sold.',

  'dash.booksOpen': 'The books are open, so these figures can still move.',
  'dash.booksClosed': 'The books are closed. These are the figures that were reported.',
  'dash.booksClosedOn':
    'The books are closed on {date}. These are the figures that were reported.',
  'dash.booksNotOpened':
    'Nobody has opened the books for this month, so nothing can post into it.',
  'dash.booksUnknown': 'Whether the books are open is not yours to see.',

  'dash.whatTheMonthMade': 'What the month made',
  'dash.totalGross': 'Total gross',
  'dash.financeShort': 'F&I',
  'dash.whatSold': 'What sold',
  'dash.carsDelivered': 'Cars delivered',
  'dash.jobsInvoiced': 'Jobs invoiced',
  'dash.grossPerCar': 'Gross per car',
  'dash.frontAndBack': 'front and back together',

  'dash.whereGrossCameFrom': 'Where the gross came from',
  'dash.colDepartment': 'Department',
  'dash.colRevenue': 'Revenue',
  'dash.colCost': 'Cost',
  'dash.colGross': 'Gross',
  'dash.colMargin': 'Margin',
  'dash.total': 'Total',

  'dash.howOldTheStockIs': 'How old the stock is',
  'dash.unsoldAsAt': ' — {count} unsold, as at {date}',
  'dash.nothingUnsold': 'Nothing unsold on the lot.',
  'dash.standingLongest': 'Standing longest',
  'dash.colStock': 'Stock',
  'dash.colVehicle': 'Vehicle',
  'dash.colStatus': 'Status',
  'dash.colDays': 'Days',
  'dash.estimatedAge':
    'No acquisition date was recorded, so this counts from when it was entered.',
  'dash.estimatedAgeNote':
    '* counted from when the car was entered, because no acquisition date was recorded.',

  // --- The workshop ---------------------------------------------------------
  'workshop.title': 'Workshop',
  'workshop.loading': 'Loading the workshop…',
  'workshop.denied':
    'You do not have access to this location’s workshop. Ask a manager if you think that is wrong.',
  'workshop.failed': 'The workshop list could not be read.',
  'workshop.actionFailed': 'That did not work.',
  'workshop.openOnly': 'Only what is still open',
  'workshop.nothingOpen': 'Nothing is in the workshop right now.',
  'workshop.empty': 'No jobs here yet.',
  'workshop.caption': 'Jobs in the workshop at the locations you cover.',
  'workshop.colJob': 'Job',
  'workshop.colCustomer': 'Customer',
  'workshop.colVehicle': 'Vehicle',
  'workshop.colCameInFor': 'Came in for',
  'workshop.colWaiting': 'Waiting',
  'workshop.colDue': 'Due',
  'workshop.colStage': 'Stage',

  'workshop.waitingTitle': 'Waiting on a customer',
  // The whole sentence per plural form, because the second half changes with
  // the count too — "it cannot" against "none of them can".
  'workshop.waitingNote': {
    one: 'One job has work nobody has agreed to pay for yet. It cannot be invoiced until somebody rings.',
    other:
      '{count} jobs have work nobody has agreed to pay for yet. None of them can be invoiced until somebody rings.',
  },
  'workshop.toAskAbout': {
    one: '{count} job to ask about',
    other: '{count} jobs to ask about',
  },
  'workshop.pendingNote': {
    one: 'One piece of work is waiting on the customer. It cannot be billed until they answer.',
    other:
      '{count} pieces of work are waiting on the customer. None of them can be billed until they answer.',
  },

  // How a job's stage reads on screen, and how the button that moves it there
  // reads. Deliberately different words: "Work finished" is a state, "Work is
  // finished" is somebody telling the system so.
  'workshop.stageBooked': 'Booked in',
  'workshop.stageInProgress': 'Being worked on',
  'workshop.stageCompleted': 'Work finished',
  'workshop.stageInvoiced': 'Invoiced',
  'workshop.stageCancelled': 'Cancelled',

  'workshop.moveInProgress': 'Start work',
  'workshop.moveCompleted': 'Work is finished',
  'workshop.moveInvoiced': 'Invoice it',
  'workshop.moveCancelled': 'Cancel the job',
  'workshop.moveBooked': 'Back to booked',

  'workshop.miles': '{count} miles',
  'workshop.bookedIn': 'booked in {date}',
  'workshop.printJobSheet': 'Print the job sheet',
  'workshop.printInvoice': 'Print the invoice',
  'workshop.whatHappened': 'What happened',

  'workshop.theWork': 'The work',
  'workshop.nothingWrittenUp': 'Nothing written up yet.',
  'workshop.colWhat': 'What',
  'workshop.colDetail': 'Detail',
  'workshop.colAgreed': 'Agreed?',
  'workshop.colAmount': 'Amount',
  'workshop.nobodyAsked': 'Nobody has asked',
  'workshop.saidNo': 'Said no',
  'workshop.agreed': 'Agreed',
  'workshop.iRangThem': 'I rang them',
  'workshop.notNow': 'Not now',
  'workshop.howObtained': 'How it was obtained',
  'workshop.howObtainedPlaceholder': 'Phoned 10:40, spoke to Mrs Okafor',
  'workshop.theySaidYes': 'They said yes',
  'workshop.theySaidNo': 'They said no',
  'workshop.hoursAtRate': '{hours} h at {rate}',

  'workshop.writeUpMore': 'Write up more work',
  'workshop.lineKind': 'What',
  'workshop.lineDescription': 'Description',
  'workshop.hours': 'Hours',
  'workshop.rate': 'Rate',
  'workshop.amount': 'Amount',
  'workshop.addLine': 'Add it',

  'workshop.totalsCaption': 'What the job comes to.',
  'workshop.totalLabour': 'Labour',
  'workshop.totalParts': 'Parts',
  'workshop.totalSublet': 'Sent out',
  'workshop.totalDue': 'Due',

  'workshop.whoIsOnIt': 'Who is on it',
  'workshop.nobodyYet': 'Nobody yet',
  'workshop.caption2': 'The work at the locations you cover, newest first, up to {limit}.',
  'workshop.invoicedNothingMore': 'Invoiced {date}. Nothing more to do.',
  'workshop.jobFinished': 'This job is finished.',
  'workshop.assignedElsewhere':
    'Assigned to somebody who is not on your staff list — they may work at another location.',

  // --- Getting back into an account -----------------------------------------
  // Read by somebody locked out and probably annoyed. Every sentence says what
  // to do next; none of them speculates about what went wrong, because the
  // server deliberately does not say (ADR-018).
  'recover.link': 'I have forgotten my password',
  'recover.title': 'Get back in',
  'recover.lede':
    'Choose how you can prove the account is yours. Whichever you use, you set a new password here and now.',
  'recover.withAuthenticator': 'Use my authenticator app',
  'recover.withAuthenticatorHint':
    'For anybody set up with two-step sign-in. A code from the app, or one of the recovery codes you saved.',
  'recover.withCode': 'Use a code from my manager',
  'recover.withCodeHint':
    'Ask a manager to issue one from the People screen. They read it out; it lasts four hours.',
  'recover.email': 'Email',
  'recover.codeFromApp': 'Code from your authenticator app',
  'recover.codeFromManager': 'The code your manager gave you',
  'recover.newPassword': 'New password',
  'recover.newPasswordAgain': 'New password again',
  'recover.mismatch': 'Those two do not match.',
  'recover.submit': 'Set my password',
  'recover.working': 'Setting it…',
  'recover.doneTitle': 'That is done',
  'recover.doneLede':
    'Your password is changed and every device that was signed in has been signed out. Sign in again with the new one.',
  'recover.toSignIn': 'Go to sign in',
  'recover.back': 'Choose a different way',
  'recover.noMethods':
    'This installation has no way to recover an account on its own. Ask a manager to set you up again.',

  // --- Handing out a reset (the People screen) ------------------------------
  'staff.resetTitle': 'Reset their password',
  'staff.reset': 'Issue a reset code',
  'staff.resetting': 'Issuing…',
  'staff.resetNote':
    'This lets them sign in as themselves again. Read the code out — it is shown once and lasts four hours.',
  'staff.resetWarning':
    'You are handing over the ability to sign in as this person. Be sure it is them you are talking to.',
  'staff.resetIssued': 'A reset code was issued {when} and has not been used yet.',
  'staff.resetDone': 'I have read it out',

  // Changing a value where it is written. See shared/InlineEdit.tsx.
  'inline.changeThis': '{label}: {value}. Press to change it.',
  'inline.saved': 'Saved',

  // --- The service diary ----------------------------------------------------
  // Cars expected but not here yet. "Booking" throughout, never "appointment
  // slot" — a workshop books a car in for a morning, not for a 40-minute window.
  'enum.appointmentStatus.Scheduled': 'Expected',
  'enum.appointmentStatus.Arrived': 'Arrived',
  'enum.appointmentStatus.NoShow': 'Did not come',
  'enum.appointmentStatus.Cancelled': 'Cancelled',

  'diary.title': 'Coming in',
  'diary.loading': 'Loading the diary…',
  'diary.empty': 'Nothing booked in. The diary is clear.',
  'diary.count': {
    one: '{count} car expected',
    other: '{count} cars expected',
  },
  'diary.dayLoad': {
    one: '{count} car, {hours} h of work',
    other: '{count} cars, {hours} h of work',
  },
  'diary.dayLoadSome': {
    one: '{count} car, {hours} h booked and {unestimated} not estimated',
    other: '{count} cars, {hours} h booked and {unestimated} not estimated',
  },
  'diary.unestimated': 'Not estimated',
  'diary.colWhen': 'When',
  'diary.colCustomer': 'Customer',
  'diary.colVehicle': 'Vehicle',
  'diary.colReason': 'What for',
  'diary.colHours': 'Est.',
  'diary.colWhat': 'What now',
  'diary.itsHere': 'It’s here',
  'diary.arriving': 'Opening the job…',
  'diary.didNotCome': 'Did not come',
  'diary.becameJob': 'Job {number}',

  'diary.book': 'Book a car in',
  'diary.bookTitle': 'Book a car in',
  'diary.customer': 'Customer',
  'diary.vehicle': 'Car',
  'diary.when': 'When',
  'diary.hours': 'Hours of work expected',
  'diary.hoursHint': 'Leave blank if nobody has estimated it yet.',
  'diary.reason': 'What they are bringing it in for',
  'diary.reasonPlaceholder': 'Annual service',
  'diary.take': 'Book it',
  'diary.taking': 'Booking…',
  'diary.pickCustomer': 'Choose a customer',
  'diary.pickVehicle': 'Choose a car',

  // --- The control-plane console --------------------------------------------
  // A separate vocabulary from the dealership's, deliberately. Whoever reads
  // these screens runs the installation: they never see a car, and "dealership"
  // here means an account on a server rather than a place with a forecourt.
  // Translators — keep that distance. See AdminApp.tsx.
  'admin.badge': 'Administration',
  'admin.navDealerships': 'Dealerships',
  'admin.navSupportAccess': 'Support access',

  'admin.signInLede': 'This signs you in to the installation, not to a dealership.',
  'admin.signInCode': 'Code from your authenticator app',
  'admin.signInCodeNote': 'Leave blank only if you have not set one up yet.',

  'admin.secondFactorTitle': 'Set up your second factor',
  'admin.secondFactorRequired':
    'Administrator accounts must have one. Until you set it up, this is the only screen you can use.',
  'admin.secondFactorIntro':
    'This account can enter any dealership on this installation, so a password on its own is not enough to hold it.',
  'admin.noRecoveryCodes':
    'There are no recovery codes for an administrator account. If you lose this phone, someone with database access has to clear it for you.',

  'admin.dealerships': 'Dealerships',
  'admin.setUpDealership': 'Set up a dealership',
  'admin.suspendConfirm':
    'Suspend {name}? Everyone there is signed out of the system immediately and cannot work until it is resumed.',
  'admin.dealershipReady': '{name} is ready',
  'admin.firstManager':
    'Their first manager is {email}. Read this code out to them — they set their own password with it at the sign-in screen.',
  'admin.codeShownOnce': 'This is the only time it can be shown.',
  'admin.codeShownOnceWhy':
    'Only a scrambled copy is kept, so it cannot be looked up again — if it goes astray, the manager can be issued a new one from the dealership’s own People screen. The books are open, so they can trade straight away.',
  'admin.passedItOn': 'I have passed it on',
  'admin.loadingDealerships': 'Loading the dealership list…',
  'admin.noDealerships': 'No dealerships on this installation yet. Set the first one up above.',
  'admin.dealershipsCaption': {
    one: '{count} dealership on this installation',
    other: '{count} dealerships on this installation',
  },
  'admin.colName': 'Name',
  'admin.colKey': 'Key',
  'admin.colStatus': 'Status',
  'admin.colSchema': 'Schema',
  'admin.colInService': 'In service',
  'admin.resume': 'Resume',
  'admin.suspend': 'Suspend',

  'admin.setUpNote':
    'This creates their database, opens their books for this month, and creates one manager who then adds everybody else. You will never see or choose their password.',
  'admin.dealershipName': 'Dealership name',
  'admin.shortName': 'Short name',
  'admin.shortNameHint':
    'Lowercase letters, digits and hyphens. Their staff type this to sign in, and it cannot be changed afterwards.',
  'admin.firstLocation': 'First location',
  'admin.firstLocationPlaceholder': 'Main site',
  'admin.locationCode': 'Location code',
  'admin.managerName': 'Manager’s name',
  'admin.managerEmail': 'Manager’s email',
  'admin.setItUp': 'Set it up',
  'admin.settingItUp': 'Setting it up…',
  'admin.notCreated': 'The dealership was not created.',

  'admin.supportAccess': 'Support access',
  'admin.supportEnter': 'Enter a dealership',
  'admin.supportLede':
    'You will be able to read their records and change nothing, for an hour at most. They see this in their own log, with your name and the reason you give here.',
  'admin.supportDealership': 'Dealership',
  'admin.supportReason': 'Why you need to go in',
  'admin.supportReasonNote':
    'This is recorded permanently, in their log as well as ours. Write what you would be willing to have them read.',
  'admin.supportOpen': 'Open access',
  'admin.supportOpening': 'Opening…',
  'admin.supportOpened':
    'You are in {tenant} until {time}. Open the dealership’s screens in this browser to look; close the visit below when you are done.',
  'admin.supportRecord': 'The record',
  'admin.supportLoading': 'Loading the record…',
  'admin.supportEmpty': 'Nobody has been into a dealership yet.',
  'admin.supportCaption': {
    one: '{count} support visit',
    other: '{count} support visits, newest first',
  },
  'admin.supportColWho': 'Who',
  'admin.supportColWhy': 'Why',
  'admin.supportColOpened': 'Opened',
  'admin.supportColState': 'State',
  'admin.supportCloseNow': 'Close now',
  'admin.supportExpired': 'Expired',
  'admin.supportClosed': 'Closed {date}',
} as const;
