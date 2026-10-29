import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';

import { AuthGuard } from './auth.guard';

export const authOrDemoGuard: CanActivateFn = (route) => {
  if (route.queryParamMap.has('demo')) {
    return true;
  }

  return inject(AuthGuard).canActivate(route);
};
