# RestaurantFrontendApp

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 19.2.15.

## Owner group pre-order

The Owner screen shows each guest's basket and submission status, totals the submitted baskets into one order, and submits that consolidated order to the restaurant.

Run the mock flow locally without an account or backend:

```bash
cd frontend/restaurant-frontend-app
npm ci
npm start
```

Open `http://localhost:4200/reservations/demo-reservation/group-order?demo`. Demo data and submission are handled locally; no API requests are made. For an authenticated reservation, open `/reservations/<reservation-id>/group-order`; live guest sessions refresh every 15 seconds and the final submit uses the group-reservation API.

Run the focused homework checks with `npm run test:hw`.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Karma](https://karma-runner.github.io) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
