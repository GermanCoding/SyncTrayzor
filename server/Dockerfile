FROM php:8.5-apache@sha256:1d825469e3c69b10baf6a8a63b1e766b81c6edb44a01f91509fc148bfd0e5f3e
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80