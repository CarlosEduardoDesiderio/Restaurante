import "./globals.css";

export const metadata = {
  title: "Restaurante Inteligente",
  description: "Gestão inteligente para restaurantes"
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR">
      <body>{children}</body>
    </html>
  );
}
