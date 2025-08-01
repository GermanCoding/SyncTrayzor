FROM php:8.4-apache@sha256:d79e472947b150b581240b8d16ba234c0dd3a6a484881ebbed3bc9f53c81bd0b
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80