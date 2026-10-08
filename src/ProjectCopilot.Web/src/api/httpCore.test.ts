import { describe, expect, it } from 'vitest';
import { fakeResponse } from '../test/fakeResponse';
import { parseJsonResponseOrThrow } from './httpCore';
import { ApiError } from './types';

describe('parseJsonResponseOrThrow', () => {
  it('preserves field-level validation errors on ApiError.body, not just title/detail', async () => {
    // Shape of a real ASP.NET Core ValidationProblemDetails response: no top-level
    // `detail`, just `errors` keyed by field name - this is exactly what a FluentValidation
    // failure (e.g. CreateProjectValidator's deadline rule) produces.
    const response = fakeResponse(400, {
      title: 'Validation failed.',
      status: 400,
      errors: { Deadline: ['Deadline must not be before today.'] },
    });

    let thrown: unknown;
    try {
      await parseJsonResponseOrThrow(response, 'fallback');
    } catch (error) {
      thrown = error;
    }

    expect(thrown).toBeInstanceOf(ApiError);
    const apiError = thrown as ApiError;
    // The field error must survive onto `body`, not be discarded.
    expect(apiError.body?.errors?.Deadline).toEqual(['Deadline must not be before today.']);
    // No top-level `detail` in this shape, so the generic `.message` still falls back to
    // `title` - unchanged existing behavior, callers that want field-specific feedback must
    // read `.body.errors` themselves (see ProjectsPage.getCreateProjectErrorMessage).
    expect(apiError.message).toBe('Validation failed.');
  });

  it('falls back to title when there are no field errors at all', async () => {
    const response = fakeResponse(500, { title: 'Server error.', status: 500 });

    let thrown: unknown;
    try {
      await parseJsonResponseOrThrow(response, 'fallback');
    } catch (error) {
      thrown = error;
    }

    expect(thrown).toBeInstanceOf(ApiError);
    const apiError = thrown as ApiError;
    expect(apiError.body?.errors).toBeUndefined();
    expect(apiError.message).toBe('Server error.');
  });
});
