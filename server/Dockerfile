FROM php:8.5-apache@sha256:6e5cfaec7df961fe2cca048e9ce3a3c448c1b77b489c51c4e1d98cda943759af
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80