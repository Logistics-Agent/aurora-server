import { createParamDecorator, ExecutionContext } from '@nestjs/common';
import { CurrentUser } from './current-user.interface';

export const UserContext = createParamDecorator(
  (data: unknown, ctx: ExecutionContext): CurrentUser => {
    const request = ctx.switchToHttp().getRequest();
    return request.user;
  },
);
