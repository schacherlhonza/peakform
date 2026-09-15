import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '../test/renderWithProviders';
import { LoginPage } from './LoginPage';

describe('LoginPage', () => {
  it('renders the login form', () => {
    renderWithProviders(<LoginPage />, { route: '/login' });

    expect(screen.getByText('Přihlášení')).toBeInTheDocument();
    expect(screen.getByLabelText('E-mail')).toBeInTheDocument();
    expect(screen.getByLabelText('Heslo')).toBeInTheDocument();
  });

  it('shows a validation error for an invalid email instead of submitting', async () => {
    const user = userEvent.setup();
    renderWithProviders(<LoginPage />, { route: '/login' });

    await user.type(screen.getByLabelText('E-mail'), 'not-an-email');
    await user.type(screen.getByLabelText('Heslo'), 'somepassword');
    await user.click(screen.getByRole('button', { name: 'Přihlásit se' }));

    await waitFor(() => {
      expect(screen.getByLabelText('E-mail')).toBeInvalid();
    });
  });

  it('links to the registration page', () => {
    renderWithProviders(<LoginPage />, { route: '/login' });

    expect(screen.getByRole('link', { name: 'Zaregistrujte se' })).toHaveAttribute('href', '/register');
  });
});
