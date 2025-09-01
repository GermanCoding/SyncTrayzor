FROM php:8.4-apache@sha256:fc535e6bf80041c11d99380d2a5a3ff4ceaafac7efbd4f8d53c876ca4751c7c2
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80