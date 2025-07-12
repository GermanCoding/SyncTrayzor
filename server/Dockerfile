FROM php:8.4-apache@sha256:da55a9714827cfa0c30181fee901c1bf2d5d914d12143769f4408215cbc4afbc
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80